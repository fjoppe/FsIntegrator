namespace Camel.FileHandling

open System
open Camel.Core
open Camel.Core.EngineParts
open System.Threading
open NLog

/// Contains "SubRoute" configuration
type Properties = {
        Id           : Guid
        Name         : string
//        Options      : Options
    }
    with
    static member Create path = 
        {Id = Guid.NewGuid(); Name = path}


/// Contains "File" state
type State = {
        ProducerHook : ProducerMessageHook option
        RunningState    : ProducerState
        Cancellation    : CancellationTokenSource
        EngineServices  : IEngineServices option
    }
    with
    static member Create convertedOptions = 
        {ProducerHook = None; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None}

    member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
    member this.SetEngineServices services = {this with EngineServices = services}


#nowarn "0050"  // warning that implementation of some interfaces are invisible because absent in signature. But that's exactly what we want.
type SubRoute(props : Properties, state: State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger("debug"); 

    /// Change this instance's state
    let changeState (newState:State) = SubRoute(props, newState)

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail action =
        if state.ProducerHook.IsSome then action
        else raise (MissingMessageHook(sprintf "File with path '%s' has no producer hook." props.Path))

    member private this.Properties with get() = props
    member private this.State with get() = state

    /// Change the running state of this instance
    member private this.changeRunningState targetState action = 
        if state.RunningState = targetState then this :> IProducerDriver
        else 
            witProducerHookOrFail (
                let intermediateState = action()
                changeState {intermediateState with RunningState = targetState} :> IProducerDriver
            )

    /// State File polling
    member this.startFilePolling() = 
        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop() = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            match state.ProducerHook with
            | Some(sendToRoute) ->
                try
                    Directory.GetFiles(configUri.LocalPath)  |> List.ofArray
                        |> List.map(fun filename -> new FileInfo(filename))
                        |> List.sortBy (fun fileInfo -> fileInfo.CreationTimeUtc)
                        |> List.iter(fun fileInfo -> 
                            match state.TaskPool with
                            |   None      ->  processFile fileInfo sendToRoute
                            |   Some pool ->
                                //  this will block execution, until there is a token in the pool
                                let token = pool.AsyncGet() |> Async.RunSynchronously
                                //  process async, to enable the next iteration for List.iter
                                async {
                                    try
                                        processFile fileInfo sendToRoute
                                    with
                                    |   e -> printfn "%A" e;  logger.Error e
                                    //  always release the toke to the pool
                                    pool.AsyncAdd(token) |> Async.RunSynchronously
                                } |> Async.Start
                            )
                with
                |   e -> printfn "%A" e; logger.Error e

            | None -> Async.Sleep (WaitForHook*1000) |> Async.RunSynchronously
            return! loop()
        }
        state.Timer.Start()
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)
        state


    member this.stopFilePolling() = 
        state.Timer.Stop()
        state.Cancellation.Cancel()
        {state with Cancellation = new CancellationTokenSource()}   // the CancellationToken is not reusable, so we make this for the next "start"


    new(path, optionList) =
        let options = Properties.Create path optionList
        let fileState = State.Create <| options.Options
        File(options, fileState)


    //  Producer
    interface ``Provide a Producer Driver`` with
        override this.ProducerDriver with get() = this :> IProducerDriver
    interface IProducerDriver with        
        member this.Start() = this.changeRunningState Running (this.startFilePolling)
        member this.Stop() =  this.changeRunningState Stopped (this.stopFilePolling)
        member this.SetProducerHook hook = changeState { state with ProducerHook = Some(hook) } :> IProducerDriver
        member this.RunningState with get () = state.RunningState
        member this.Register services =  changeState (state.SetEngineServices (Some services)) :> IProducerDriver
        member this.Validate() =
            match state.EngineServices with
            |   None        -> false
            |   Some engine ->
                let fileListenerList = engine.producerList<File>()
                let invalid = 
                    fileListenerList |>  List.tryPick(fun fp -> 
                        let refId = fp.Properties.Id
                        let refPath = fp.Properties.Path
                        let foundList = 
                            fileListenerList 
                            |> List.filter(fun i -> i.Properties.Id <> refId)
                            |> List.filter(fun i -> i.Properties.Path = refPath)
                        if foundList.Length = 0 then None
                        else Some(foundList.Head)
                    )
                invalid.IsNone

    //  Consumer
    member private this.writeFile (message:Message) =
        let configUri = new Uri(props.Path)
        File.WriteAllText(configUri.AbsolutePath, message.Body)

    member private this.Consume (message:Message) =
        this.writeFile message

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver with get() = this :> IConsumerDriver
    interface IConsumerDriver with
        member self.GetConsumerHook with get() = this.Consume