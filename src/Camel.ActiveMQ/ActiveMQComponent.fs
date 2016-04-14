namespace Camel.Queing

open System
open System.Threading
open Apache.NMS
open Apache.NMS.Util
open Apache.NMS.ActiveMQ.Commands
open NLog
open Camel.Core
open Camel.Core.General
open Camel.Core.EngineParts
open Camel.Utility

exception ActiveMQComponentException of string

type DestinationType =
    |   Queue
    |   Topic


type AMQOption =
    |   Connection  of string
    |   Credentials of Credentials
    |   DestinationType of DestinationType
    |  ConcurrentTasks of int


type Properties = {
        Id              : Guid
        Destination     : string
        Connection      : Uri
        Credentials     : Credentials option
        DestinationType : DestinationType
        ConcurrentTasks : int
    }
    with
    static member convertOptions options =
        let defaultOptions = {
            Id = Guid.NewGuid()
            Destination = String.Empty
            DestinationType = DestinationType.Queue
            Connection = new Uri("urn:invalid")
            Credentials = None
            ConcurrentTasks = 0
        }
        options 
        |> List.fold (fun state option ->
            match option with
            |   Connection(c)       -> {state with Connection = new Uri(c)}
            |   Credentials(c)      -> {state with Credentials = Some(c)}
            |   DestinationType(d)  -> {state with DestinationType = d}
            |   ConcurrentTasks(amount)  -> 
                if amount > 0 then
                    {state with ConcurrentTasks = amount}
                else
                    raise <| ActiveMQComponentException "ERROR: ConcurrentTasks must be larger than 0"
        ) defaultOptions

    static member Create destination options = 
        let convertedOptions = Properties.convertOptions options
        {convertedOptions with Destination = destination }    


type State = {
        ProducerHook    : ProducerMessageHook option
        RunningState    : ProducerState
        Cancellation    : CancellationTokenSource
        EngineServices  : IEngineServices option
        Connection      : IConnection option
        Session         : ISession option
        TaskPool        : RestrictedResourcePool
    }
    with
    static member Create convertedOptions = 
        {ProducerHook = None; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None; Connection = None; Session = None; TaskPool = RestrictedResourcePool.Create <| convertedOptions.ConcurrentTasks}

    member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
    member this.SetEngineServices services = {this with EngineServices = services}


type Operation =
    |   SetProducerHook of ProducerMessageHook * ActionAsyncResponse
    |   SetEngineServices of IEngineServices   * ActionAsyncResponse
    |   ChangeRunningState of ProducerState  * (State -> State)  * ActionAsyncResponse
    |   GetRunningState of FunctionsAsyncResponse<ProducerState>
    |   GetSession of FunctionsAsyncResponse<ISession>


#nowarn "0050"  // warning that implementation of "RouteEngine.IProducer'" is invisible because absent in signature. But that's exactly what we want.
type ActiveMQ(props : Properties, initialState : State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger(this.GetType().FullName); 

    //  Utility functions
    let changeState (newState:State) = ActiveMQ(props, newState)

    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "ActiveMQ consumer '%s' has no producer hook." props.Destination))

    let convertToActiveMQ (dt:DestinationType) : Apache.NMS.DestinationType =
        match dt with
        |   Queue   -> Apache.NMS.DestinationType.Queue
        |   Topic   -> Apache.NMS.DestinationType.Topic

    let getActiveMQConnection() =
        let connecturi = props.Connection
        let factory = NMSConnectionFactory(connecturi)
        let connection = 
            match props.Credentials with
            | Some(c)   -> factory.CreateConnection(c.Username,c.Password)
            | None      -> factory.CreateConnection()
        connection

    let changeRunningState state targetState action = 
        if state.RunningState = targetState then state
        else 
            let newState = witProducerHookOrFail state action
            logger.Debug(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    //  Start listening for messages
    let startListening state =
        let connection = getActiveMQConnection()
        let session = connection.CreateSession(AcknowledgementMode.ClientAcknowledge)
        let destination = SessionUtil.GetDestination(session, props.Destination, convertToActiveMQ props.DestinationType)

        let consumer = session.CreateConsumer(destination)

        let getXmlContent (m:IMessage) =
            match m with
            |   :? ActiveMQTextMessage as s -> s.Text
            |   _   -> let err = sprintf "unexpected message: %A" m
                       logger.Error err
                       raise (MessageFormatException err)

        let sendMessage send (amqMessage:IMessage)  = 
            try
                let content = getXmlContent amqMessage
                let message = Message.Empty.SetBody content
                send message
                amqMessage.Acknowledge()
            with
            |   MessageFormatException(s) -> logger.Error "Skipping"

        let processMessage send =
            let amqMessage = consumer.Receive()
            sendMessage send amqMessage


        let rec loop() = async {
            let sendToRoute = state.ProducerHook.Value
            try
                state.TaskPool.PooledAction(fun () -> processMessage sendToRoute)
            with
            |   e -> printfn "%A" e
                     logger.Error e
            return! loop()
        }
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)       
        connection.Start()
        logger.Debug(sprintf "Started ActiveMQ Listener for destination: %s - %A" props.Destination props.DestinationType)
        { state with Connection = Some connection; Session = Some session}

        
    let stopListening state = 
        state.Cancellation.Cancel()
        match state.Session with
        |   Some session  -> session.Close() ; session.Dispose()
        |   None          -> 
            let msg = "ActiveMQ has no Session set, this may never occur!"
            logger.Error(msg)
            printfn "%s"msg

        match state.Connection with
        |   Some connection  -> connection.Close() ; connection.Dispose()
        |   None             -> 
            let msg = "ActiveMQ has no Connection set, this may never occur!"
            logger.Error(msg)
            printfn "%s"msg
        logger.Debug(sprintf "Stopped ActiveMQ Listener for destination: %s - %A" props.Destination props.DestinationType)
        { state with Connection = None; Session = None; Cancellation = new CancellationTokenSource()} // the CancellationToken is not reusable, so we make this for the next "start"


    let agent = 
        let newAgent = new Agent<Operation>(fun inbox ->
            ///#region let actionReply (state:State) replychannel (action:unit->State) -> State   // executes action, responds via replychannel, on success returns changed "state"
            let actionReply state (replychannel:ActionAsyncResponse) action : State =
                try
                    let newState = action()
                    replychannel.Reply OK
                    newState
                with
                |   e -> replychannel.Reply(ActionResponse.ERROR e)
                         state
            ///#endregion

            ///#region let getOrCreate value action // gets data for value=Some(data) or creates with action() when value=None
            let getOrCreate value action =
                match value with
                |   None        -> action()
                |   Some data   -> data
            ///#endregion

            let rec loop (state:State) = 
                async {
                    try
                        logger.Debug "Waiting for message.."
                        let! command = inbox.Receive()
                        match command with
                        |   SetProducerHook (hook, replychannel) -> 
                            logger.Debug "SetProducerHook"
                            return! loop <| actionReply state replychannel (fun () -> state.SetProducerHook hook)
                        |   SetEngineServices (svc, replychannel) -> 
                            logger.Debug "SetEngineServices"
                            return! loop <| actionReply state replychannel (fun () -> state.SetEngineServices (Some svc))
                        |   ChangeRunningState (targetState, action, replychannel) ->
                            logger.Debug(sprintf "ChangeRunningState to: %A" targetState)
                            return! loop <| actionReply state replychannel (fun () -> changeRunningState state targetState (fun () -> action state))
                        |   GetRunningState replychannel ->
                            logger.Debug "GetRunningState"
                            replychannel.Reply <| Response(state.RunningState)
                            return! loop state
                        |   GetSession replychannel ->
                            logger.Debug "GetSession"
                            let connection = getOrCreate (state.Connection) (fun () -> getActiveMQConnection())
                            let session = getOrCreate (state.Session) (fun () -> connection.CreateSession(AcknowledgementMode.ClientAcknowledge))
                            replychannel.Reply <| Response(session)
                            return! loop {state with Connection = Some(connection); Session = Some(session)}                                
                    with
                    |   e -> logger.Error(sprintf "Uncaught exception: %A" e)
                    return! loop state
                }
            loop initialState)
        newAgent.Start()
        newAgent

    member private this.Properties with get() = props
    member private this.State with get() = initialState


    new(destination, optionList) =
        let options = Properties.Create destination optionList
        let componentState = State.Create options
        ActiveMQ(options, componentState)

    //  =============================================== Producer ===============================================
    interface ``Provide a Producer Driver`` with
        override this.ProducerDriver with get() = this :> IProducerDriver

    interface IProducerDriver with
        member this.Start() = agent.PostAndReply(fun replychannel -> ChangeRunningState(Running, startListening, replychannel)) |> ignore
        member this.Stop() =  agent.PostAndReply(fun replychannel -> ChangeRunningState(Stopped, stopListening, replychannel)) |> ignore
        member this.SetProducerHook hook = agent.PostAndReply(fun replychannel -> SetProducerHook(hook, replychannel)) |> ignore

        member this.RunningState 
            with get () = 
                let result = agent.PostAndReply(fun replychannel -> GetRunningState(replychannel))
                result.GetResponseOrRaise()

        member this.Validate() = true


    //  ===============================================  Consumer  ===============================================
    member private this.Consume (producer:IMessageProducer) (message:Camel.Core.General.Message) =        
        try
            logger.Debug(sprintf "Send message to %s - %A" props.Destination props.DestinationType)
            let messageToSend = producer.CreateTextMessage(message.Body)
            producer.Send(messageToSend)
        with
        |   e ->
            logger.Error(sprintf "Error: %A" e)
            reraise()

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver with get() = this :> IConsumerDriver


    interface IConsumerDriver with
        member self.GetConsumerHook
            with get() =
                logger.Debug("GetConsumerHook.get()")
                let sessionResponse = agent.PostAndReply(fun replychannel -> GetSession(replychannel))
                let session = sessionResponse.GetResponseOrRaise()
                let destination = SessionUtil.GetDestination(session, props.Destination, convertToActiveMQ props.DestinationType)
                let producer = session.CreateProducer(destination)
                this.Consume producer

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore
