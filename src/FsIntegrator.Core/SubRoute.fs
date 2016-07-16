namespace FsIntegrator

open System
open System.Threading
open NLog
open FsIntegrator
open FsIntegrator.RouteEngine
open FsIntegrator.MessageOperations
open FsIntegrator.Utility

exception SubRouteException of string


type SubRouteOption =
     |  FailForMissingActiveSubRoute of bool


module SubRouteInternal =

    type Options = {
         FailForMissingActiveSubRoute : bool
    }

    type PathType =
        |   Fixed       of string
        |   Evaluate    of StringMacro

    /// Contains "SubRoute" configuration
    type Properties = {
            Id           : Guid
            Name         : PathType
            Options      : Options
        }
        with
        static member convertOptions options =
            let defaultOptions = {
                FailForMissingActiveSubRoute = false;
                }
            options 
            |> List.fold (fun state option ->
                match option with
                |   FailForMissingActiveSubRoute b -> {state with FailForMissingActiveSubRoute = b}
            ) defaultOptions

        static member Create path options = 
            let convertedOptions = Properties.convertOptions options
            {Id = Guid.NewGuid(); Name = path; Options = convertedOptions}

    type SubRouteMessage = Message * FunctionsAsyncResponse<Message>

    /// Contains "SubRoute" state
    type State = {
            ProducerHook    : ProducerMessageHook option
            RunningState    : ProducerState
            Cancellation    : CancellationTokenSource
            EngineServices  : IEngineServices option
            Driver          : Agent<SubRouteMessage> option
        }
        with
        static member Create convertedOptions = 
            {ProducerHook = None; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None; Driver = None}

        member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
        member this.SetEngineServices services = {this with EngineServices = services}


    type Operation =
        |   SetProducerHook of ProducerMessageHook * ActionAsyncResponse
        |   SetEngineServices of IEngineServices   * ActionAsyncResponse
        |   ChangeRunningState of ProducerState  * (State -> State)  * ActionAsyncResponse
        |   GetRunningState of FunctionsAsyncResponse<ProducerState>
        |   GetEngineServices of FunctionsAsyncResponse<IEngineServices>
        |   GetDriver of FunctionsAsyncResponse<Agent<SubRouteMessage>>


open SubRouteInternal
#nowarn "0050"  // warning that implementation of some interfaces are invisible because absent in signature. But that's exactly what we want.
type SubRoute(props : Properties, initialState: State) as this = 

    let logger = LogManager.GetLogger(this.GetType().FullName); 

    let getName (message: Message option) =
        match props.Name with
        |   Fixed str   -> str
        |   Evaluate strmacro ->
            match message with
            |   Some    (msg) -> strmacro.Substitute(msg)
            |   None          -> raise <| SubRouteException("Cannot evaluate StringMacro without message")

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "SubRoute with name '%s' has no producer hook." (getName None)))


    /// Change the running state of this instance
    let changeRunningState state targetState (action: unit -> State) = 
        if state.RunningState = targetState then state
        else 
            let newState = witProducerHookOrFail state action
            logger.Trace(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    /// State Subroute
    let startListening state = 
        let sendToRoute = state.ProducerHook.Value
        let agent = 
            new Agent<SubRouteMessage>((fun inbox ->
                let rec loop() = async {
                    let! message, replyChannel = inbox.Receive()
                    try
                        let result = sendToRoute message
                        replyChannel.Reply(Response(result))
                    with
                    |   e -> 
                        replyChannel.Reply(ERROR(e))
                    return! loop()
                }
                loop()
            ),cancellationToken = state.Cancellation.Token            
        )
        agent.Start()
        logger.Debug(sprintf "Started Subroute listener: '%s'" (getName None))
        {state with Driver = Some(agent)}

    let stopListening state = 
        state.Cancellation.Cancel()
        logger.Debug(sprintf "Stopped Subroute listener: '%s'" (getName None))
        {state with Cancellation = new CancellationTokenSource()}   // the CancellationToken is not reusable, so we make this for the next "start"



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

            let rec loop (state:State) = 
                async {
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
                    |   GetEngineServices replychannel ->
                        logger.Trace "GetEngineServices"
                        match state.EngineServices with
                        |   None       -> replychannel.Reply <| ERROR(SubRouteException("This consumer is not connected with a route-engine"))
                        |   Some value -> replychannel.Reply <| Response(value)
                        return! loop state
                    |   GetDriver replychannel ->
                        logger.Trace "GetDriver"
                        match state.Driver with
                        |   None        -> replychannel.Reply <| ERROR(SubRouteException(sprintf "ERROR: Subroute does not have an active listener for '%s' - this is a framework problem, this may never occur." (getName None)))
                        |   Some driver -> replychannel.Reply <| Response(driver)
                        return! loop state
                }
            loop initialState)
        newAgent.Start()
        newAgent

    member private this.Properties with get() = props
    member private this.State with get() = initialState
    member private this.Agent with get() = agent

    new(path, optionList : SubRouteOption list) =
        let options = Properties.Create path optionList
        let fileState = State.Create <| options.Options
        SubRoute(options, fileState)

    new(path, optionList : SubRouteOption list) =
        SubRoute((Fixed path), optionList)

    new(path, optionList : SubRouteOption list) =
        SubRoute((Evaluate path), optionList)

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

        member this.Validate() =
            logger.Trace("Validate()")
            match initialState.EngineServices with
            |   None        -> false
            |   Some engine ->
                let producerList = engine.producerList<SubRoute>()

                let invalid =
                    match props.Name with
                    |   Fixed propertiesName ->
                        producerList
                        |>  List.filter(fun e -> e.Properties.Id <> props.Id)
                        |>  List.exists(fun fp ->
                            match fp.Properties.Name with
                            |   Fixed refName   -> propertiesName = refName
                            |   _ -> false
                            )
                    |   _ -> true
                logger.Trace(sprintf "Is valid: %b" (not(invalid)))
                not(invalid)



    //  ===============================================  Consumer  ===============================================
    interface IConsumer
    member private this.Consume (message:Message) =
        try
            let localName = getName (Some message)
            logger.Debug(sprintf "Send message to Subroute: '%s'" localName)
            let servicesResponse = agent.PostAndReply(fun replychannel -> GetEngineServices(replychannel))
            let services = servicesResponse.GetResponseOrRaise()
            let producerList = services.producerList<SubRoute>()
            let producerCandidate = 
                producerList |> List.tryFind(
                    fun producer -> 
                        let producerRunningState = (producer :> ``Provide a Producer Driver``).ProducerDriver.RunningState
                        match producer.Properties.Name with
                        |   Fixed foreingName -> foreingName = localName && producerRunningState = ProducerState.Running
                        |   _   -> raise <| SubRouteException("There is a subroute listening to a dynamic endpoint - this should never occur - framework problem."))
            match producerCandidate with
            |   None      -> if props.Options.FailForMissingActiveSubRoute then raise <| SubRouteException(sprintf "ERROR: No active subroute found under name: %s" localName)
                             else 
                                logger.Debug(sprintf "Missing active subroute '%s', ignore and continue" localName)
                                message    // for now: ignore and continue
            |   Some producer ->  
                let driverResponse = producer.Agent.PostAndReply(fun replychannel -> GetDriver(replychannel))
                let driver = driverResponse.GetResponseOrRaise()
                let response = driver.PostAndReply(fun replyChannel -> (message, replyChannel))
                response.GetResponseOrRaise()
        with
        |   e ->
            logger.Error(sprintf "Error: %A" e)
            reraise()

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver with get() = this :> IConsumerDriver

    interface IConsumerDriver with
        member self.GetConsumerHook with get() = this.Consume

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore
