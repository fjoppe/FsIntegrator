namespace Camel.Queing

open System
open Camel.Core
open Camel.Core.General
open Camel.Core.EngineParts
open System.Threading
open Apache.NMS
open Apache.NMS.Util
open Apache.NMS.ActiveMQ.Commands
open NLog
open FSharpx.Control


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
        TaskPool        : BlockingQueueAgent<int> option
    }
    with
    static member Create convertedOptions = 
        let initial =  {ProducerHook = None; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None; Connection = None; Session = None; TaskPool = None}
        let result =
            match convertedOptions.ConcurrentTasks with
            |   1 -> initial
            |   amount -> initial.SetTaskPool(amount)
        result

    member private this.SetTaskPool size =
        let tokens = [1 .. size]
        let agent = BlockingQueueAgent<int>(size)
        tokens |> List.iter(fun item -> Async.RunSynchronously <| agent.AsyncAdd(item))
        {this with TaskPool = Some(agent)}

    member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
    member this.SetEngineServices services = {this with EngineServices = services}


#nowarn "0050"  // warning that implementation of "RouteEngine.IProducer'" is invisible because absent in signature. But that's exactly what we want.
type ActiveMQ(props : Properties, state : State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger("debug"); 

    //  Utility functions
    let changeState (newState:State) = ActiveMQ(props, newState)
    let witProducerHookOrFail statement =
        if state.ProducerHook.IsSome then statement
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


    member private this.Properties with get() = props
    member private this.State with get() = state

    member private this.changeRunningState targetState action = 
        if state.RunningState = targetState then this :> IProducerDriver
        else 
            witProducerHookOrFail (
                let intermediateState = action()
                changeState {intermediateState with RunningState = targetState} :> IProducerDriver
            )

    member this.startListening() =
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
            match state.ProducerHook with
            | Some(sendToRoute) ->
                try
                    match state.TaskPool with
                    |   None      ->  processMessage sendToRoute
                    |   Some pool ->
                        //  this will block execution, until there is a token in the pool
                        let token = pool.AsyncGet() |> Async.RunSynchronously
                        //  process async, to enable the next iteration for loop()
                        async {
                            try
                                processMessage sendToRoute
                            with
                            |   e -> printfn "%A" e;  logger.Error e
                            //  always release the toke to the pool
                            pool.AsyncAdd(token) |> Async.RunSynchronously
                        } |> Async.Start
                with
                |   e -> printfn "%A" e
                         logger.Error e
            | None -> 
                Async.Sleep (WaitForHook*1000) |> Async.RunSynchronously
            return! loop()
        }
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)       
        connection.Start()
        { state with Connection = Some connection; Session = Some session}

        
    member this.stopListening() = 
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
        { state with Connection = None; Session = None; Cancellation = new CancellationTokenSource()}


    new(destination, optionList) =
        let options = Properties.Create destination optionList
        let componentState = State.Create options
        ActiveMQ(options, componentState)

    //  =============================================== Producer ===============================================
    interface ``Provide a Producer Driver`` with
        override this.ProducerDriver with get() = this :> IProducerDriver

    interface IProducerDriver with
        member this.Start() = this.changeRunningState Running (this.startListening)
        member this.Stop() =  this.changeRunningState Stopped (this.stopListening)
        member this.SetProducerHook hook = changeState { state with ProducerHook = Some(hook) } :> IProducerDriver
        member this.RunningState with get () = state.RunningState
        member this.Register services =  changeState (state.SetEngineServices (Some services)) :> IProducerDriver
        member this.Validate() = true

    //  ===============================================  Consumer  ===============================================
    member private this.Consume (producer:IMessageProducer) (message:Camel.Core.General.Message) =        
        let messageToSend = producer.CreateTextMessage(message.Body)
        producer.Send(messageToSend)

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver 
            with get() = 
                let connection = getActiveMQConnection()
                ActiveMQ(props, {state with Connection = Some connection}) :> IConsumerDriver

    interface IConsumerDriver with
            member self.GetConsumerHook
                with get() = 
                    match state.Connection with
                    |   None -> raise(ActiveMQComponentException "No connection available, while getting consumer hook.")
                    |   Some connection ->
                        let session = connection.CreateSession(AcknowledgementMode.ClientAcknowledge)
                        let destination = SessionUtil.GetDestination(session, props.Destination, convertToActiveMQ props.DestinationType)
                        let producer = session.CreateProducer(destination)
                        this.Consume producer
