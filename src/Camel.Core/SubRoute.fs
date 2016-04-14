namespace Camel.SubRoute

open System
open Camel.Core
open Camel.Core.General
open Camel.Core.EngineParts
open Camel.Utility
open System.Threading
open NLog

exception SubRouteException of string


type SubRouteOption =
     |  FailForMissingActiveSubRoute of bool

type Options = {
     FailForMissingActiveSubRoute : bool
}

/// Contains "SubRoute" configuration
type Properties = {
        Id           : Guid
        Name         : string
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


type SubRouteMessage = Message * AsyncReplyChannel<Exception option>

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

#nowarn "0050"  // warning that implementation of some interfaces are invisible because absent in signature. But that's exactly what we want.
type SubRoute(props : Properties, initialState: State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger("debug"); 

    /// Change this instance's state
    let changeState (newState:State) = SubRoute(props, newState)

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "File with path '%s' has no producer hook." props.Name))


    /// Change the running state of this instance
    let changeRunningState state targetState (action: unit -> State) = 
        if state.RunningState = targetState then state
        else 
            let newState = witProducerHookOrFail state action
            {newState with RunningState = targetState}

    /// State Subroute
    let startListening state = 
        let sendToRoute = state.ProducerHook.Value
        let agent = 
            new Agent<SubRouteMessage>((fun inbox ->
                let rec loop() = async {
                    let! message, replyChannel = inbox.Receive()
                    try
                        sendToRoute message
                        replyChannel.Reply(None)
                    with
                    |   e -> replyChannel.Reply(Some(e))
                    return! loop()
                }
                loop()
            ),cancellationToken = state.Cancellation.Token            
        )
        agent.Start()
        {state with Driver = Some(agent)}

    let stopListening state = 
        state.Cancellation.Cancel()
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
                        return! loop <| actionReply state replychannel (fun () -> state.SetProducerHook hook)
                    |   SetEngineServices (svc, replychannel) -> 
                        return! loop <| actionReply state replychannel (fun () -> state.SetEngineServices (Some svc))
                    |   ChangeRunningState (targetState, action, replychannel) ->
                        return! loop <| actionReply state replychannel (fun () -> changeRunningState state targetState (fun () -> action state))
                    |   GetRunningState replychannel ->
                        replychannel.Reply <| Response(state.RunningState)
                        return! loop state
                    |   GetEngineServices replychannel ->
                        match state.EngineServices with
                        |   None       -> replychannel.Reply <| ERROR(SubRouteException("This consumer is not connected with a route-engine"))
                        |   Some value -> replychannel.Reply <| Response(value)
                        return! loop state
                }
            loop initialState)
        newAgent.Start()
        newAgent

    member private this.Properties with get() = props
    member private this.State with get() = initialState

    new(path, optionList) =
        let options = Properties.Create path optionList
        let fileState = State.Create <| options.Options
        SubRoute(options, fileState)


    //  Producer
    interface ``Provide a Producer Driver`` with
        override this.ProducerDriver with get() = this :> IProducerDriver
    interface IProducerDriver with        
        member this.Start() = agent.PostAndReply(fun replychannel -> ChangeRunningState(Running, startListening, replychannel)) |> ignore
        member this.Stop() =  agent.PostAndReply(fun replychannel -> ChangeRunningState(Stopped, stopListening, replychannel)) |> ignore
        member this.SetProducerHook hook = agent.PostAndReply(fun replychannel -> SetProducerHook(hook, replychannel)) |> ignore
        member this.Register services =  agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore

        member this.RunningState 
            with get () = 
                let result = agent.PostAndReply(fun replychannel -> GetRunningState(replychannel))
                result.GetResponseOrRaise()

        member this.Validate() =
            match initialState.EngineServices with
            |   None        -> false
            |   Some engine ->
                let producerList = engine.producerList<SubRoute>()
                let invalid = 
                    producerList |>  List.tryPick(fun fp -> 
                        let refId = fp.Properties.Id
                        let refName = fp.Properties.Name
                        let foundList = 
                            producerList 
                            |> List.filter(fun i -> i.Properties.Id <> refId)
                            |> List.filter(fun i -> i.Properties.Name = refName)
                        if foundList.Length = 0 then None
                        else Some(foundList.Head)
                    )
                invalid.IsNone


    //  Consumer
    member private this.Consume (message:Message) =
        let servicesResponse = agent.PostAndReply(fun replychannel -> GetEngineServices(replychannel))
        let services = servicesResponse.GetResponseOrRaise()
        let producerList = services.producerList<SubRoute>()
        let producerCandidate = producerList |> List.tryFind(fun i -> i.Properties.Name = props.Name && i.State.RunningState = ProducerState.Running)
        match producerCandidate with
        |   None      -> if props.Options.FailForMissingActiveSubRoute then raise <| SubRouteException(sprintf "ERROR: No active subroute found under name: %s" props.Name)
                            else ()    // for now: ignore and continue
        |   Some producer ->    
                match producer.State.Driver with
                |   None         -> raise <| SubRouteException(sprintf "ERROR: Subroute was found to be active, but does not have a driver for SubRoute '%s' - this is a framework problem, this may never occur." props.Name)
                |   Some  driver ->
                    let response = driver.PostAndReply(fun replyChannel -> (message, replyChannel))
                    match response with
                    |   None    -> ()
                    |   Some e  -> raise e

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver with get() = this :> IConsumerDriver
    interface IConsumerDriver with
        member self.GetConsumerHook with get() = this.Consume

