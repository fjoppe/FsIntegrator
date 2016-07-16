namespace FsIntegrator

open System
open System.Threading
open Apache.NMS
open Apache.NMS.Util
open Apache.NMS.ActiveMQ.Commands
open NLog
open FsIntegrator.Core
open FsIntegrator.RouteEngine
open FsIntegrator.MessageOperations
open FsIntegrator.Utility

exception ActiveMQComponentException of string

type DestinationType =
    |   Queue
    |   Topic


type RedeliveryPolicy = {
        MaxRedelivery   : int
        InitialDelay    : int
        Delay           : int
    }
    with
        static member Empty = {MaxRedelivery = 0; InitialDelay = 0; Delay = 0}

type AMQOption =
    |   Connection  of string
    |   Credentials of Credentials
    |   DestinationType of DestinationType
    |   ConcurrentTasks of int
    |   RedeliveryPolicy of RedeliveryPolicy
    |   EndpointFailureStrategy of EndpointFailureStrategy

module ActiveMQInternal=

    type DestinationNameType =
        |   Fixed       of string
        |   Evaluate    of StringMacro

    type Properties = {
            Id               : Guid
            Destination      : DestinationNameType
            Connection       : Uri
            Credentials      : Credentials option
            DestinationType  : DestinationType
            ConcurrentTasks  : int
            RedeliveryPolicy : RedeliveryPolicy
            EndpointFailureStrategy : EndpointFailureStrategy
        }
        with
        static member convertOptions options =
            let defaultOptions = {
                Id = Guid.NewGuid()
                Destination = Fixed(String.Empty)
                DestinationType = DestinationType.Queue
                Connection = new Uri("urn:invalid")
                Credentials = None
                ConcurrentTasks = -1
                RedeliveryPolicy = RedeliveryPolicy.Empty
                EndpointFailureStrategy = EndpointFailureStrategy.StopImmediately
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
                        raise <| ValidationException "ERROR: ConcurrentTasks must be larger than 0"
                |   RedeliveryPolicy(rp) -> {state with RedeliveryPolicy = rp}
                |   EndpointFailureStrategy(strategy) ->
                       strategy.Validate()
                       {state with EndpointFailureStrategy = strategy}
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

open ActiveMQInternal
#nowarn "0050"  // warning that implementation of "RouteEngine.IProducer'" is invisible because absent in signature. But that's exactly what we want.
type ActiveMQ(props : Properties, initialState : State) as this = 

    let logger = LogManager.GetLogger(this.GetType().FullName)

    let getDestination (message: FsIntegrator.Message option) =
        match props.Destination with
        |   Fixed str   -> str
        |   Evaluate strmacro ->
            match message with
            |   Some    (msg) -> strmacro.Substitute(msg)
            |   None          -> raise <| ActiveMQComponentException("Cannot evaluate StringMacro without message")

    //  Utility functions
    let changeState (newState:State) = ActiveMQ(props, newState)

    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "ActiveMQ consumer '%s' has no producer hook." (getDestination None)))

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
            logger.Trace(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    //  Start listening for messages
    let startListening state =
        let connection = getActiveMQConnection()

        let policy = connection.RedeliveryPolicy
        policy.MaximumRedeliveries <- props.RedeliveryPolicy.MaxRedelivery
        policy.InitialRedeliveryDelay <- props.RedeliveryPolicy.InitialDelay
        policy.RedeliveryDelay(props.RedeliveryPolicy.Delay) |> ignore


        let session = connection.CreateSession(AcknowledgementMode.ClientAcknowledge)
        
        let destination = SessionUtil.GetDestination(session, (getDestination None), convertToActiveMQ props.DestinationType)

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
                send message |> ignore
                amqMessage.Acknowledge()
            with
            |   MessageFormatException(s) -> logger.Error "Skipping"

        let processMessage send =
            let amqMessage = consumer.Receive()
            logger.Debug(sprintf "Received message from: '%s', which is a '%A'" (getDestination None) props.DestinationType)
            sendMessage send amqMessage 


        let rec loop retryCount = async {
            let sendToRoute = state.ProducerHook.Value
            try
                state.TaskPool.PooledAction(fun () -> processMessage sendToRoute)
                return! loop(None)
            with
            |   e ->
                logger.Error e
                let sleepAndLoop wt s =
                    let waitInMs = wt |> Utility.secsToMsFloat
                    Async.Sleep <| int(waitInMs) |> Async.RunSynchronously
                    loop(Some s)
                match props.EndpointFailureStrategy with
                |  EndpointFailureStrategy.WaitAndRetryInfinite wt -> return! (sleepAndLoop wt 0)
                |  EndpointFailureStrategy.WaitAndRetryCountDownBeforeStop(wt, cnt) ->
                    match retryCount with
                    |   Some(x) -> 
                        if(x < cnt) then return! (sleepAndLoop wt (x+1))
                        else (this :> IProducerDriver).Stop()
                    |   None    -> return! (sleepAndLoop wt 1)
                |  StopImmediately -> (this :> IProducerDriver).Stop()
        }
        Async.Start(loop(None), cancellationToken = state.Cancellation.Token)       
        connection.Start()
        logger.Debug(sprintf "Started ActiveMQ Listener for destination: '%s', which is a '%A'" (getDestination None) props.DestinationType)
        { state with Connection = Some connection; Session = Some session}

        
    let stopListening state = 
        state.Cancellation.Cancel()
        match state.Session with
        |   Some session  -> session.Close() ; session.Dispose()
        |   None          -> 
            let msg = "ActiveMQ has no Session set, this may never occur!"
            logger.Error(msg)

        match state.Connection with
        |   Some connection  -> connection.Close() ; connection.Dispose()
        |   None             -> 
            let msg = "ActiveMQ has no Connection set, this may never occur!"
            logger.Error(msg)
        logger.Debug(sprintf "Stopped ActiveMQ Listener for destination: %s - %A" (getDestination None) props.DestinationType)
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
                        logger.Trace "Waiting for message.."
                        let! command = inbox.Receive()
                        match command with
                        |   SetProducerHook (hook, replychannel) -> 
                            logger.Trace "SetProducerHook"
                            return! loop <| actionReply state replychannel (fun () -> state.SetProducerHook hook)
                        |   SetEngineServices (svc, replychannel) -> 
                            logger.Trace "SetEngineServices"
                            return! loop <| actionReply state replychannel (fun () -> state.SetEngineServices (Some svc))
                        |   ChangeRunningState (targetState, action, replychannel) ->
                            logger.Trace(sprintf "ChangeRunningState to: %A" targetState)
                            return! loop <| actionReply state replychannel (fun () -> changeRunningState state targetState (fun () -> action state))
                        |   GetRunningState replychannel ->
                            logger.Trace "GetRunningState"
                            replychannel.Reply <| Response(state.RunningState)
                            return! loop state
                        |   GetSession replychannel ->
                            logger.Trace "GetSession"
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

    new(destination : DestinationNameType, optionList : AMQOption list) =
        let options = Properties.Create destination optionList
        let componentState = State.Create options
        ActiveMQ(options, componentState)

    new(destination : string, optionList : AMQOption list) =
        ActiveMQ((Fixed destination), optionList)

    new(destination : StringMacro, optionList : AMQOption list) =
        ActiveMQ((Evaluate destination), optionList)

    //  =============================================== Producer ===============================================
    interface IProducer

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
    interface IConsumer
    member private this.Consume (session:ISession) (message:FsIntegrator.Message) =        
        try
            let destinationName = getDestination (Some message)
            let destination = SessionUtil.GetDestination(session, destinationName, convertToActiveMQ props.DestinationType)
            let producer = session.CreateProducer(destination)
            logger.Debug(sprintf "Send message to '%s' which is a '%A'" destinationName props.DestinationType)
            let messageToSend = producer.CreateTextMessage(message.Body)
            producer.Send(messageToSend)
            message
        with
        |   e ->
            logger.Error(sprintf "Error: %A" e)
            reraise()

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver with get() = this :> IConsumerDriver


    interface IConsumerDriver with
        member self.GetConsumerHook
            with get() =
                logger.Trace("GetConsumerHook.get()")
                let sessionResponse = agent.PostAndReply(fun replychannel -> GetSession(replychannel))
                let session = sessionResponse.GetResponseOrRaise()
                this.Consume session

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore
