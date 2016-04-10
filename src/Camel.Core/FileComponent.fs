namespace Camel.FileHandling

open System
open System.IO
open System.Threading
open System.Timers
open Camel.Core
open Camel.Core.General
open Camel.Core.EngineParts
open FileSystem
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NLog
open FSharpx.Control
open Camel.Utility

exception FileComponentException of string


type FileMessageHeader = {
        FileInfo : FileInfo
    }
    with
        static member Create fileInfo =
            { FileInfo = fileInfo}


module Internal =
    let secsToMsFloat (s:float<s>) = (s * 1000.0) / 1.0<s>


type FileOption =
     |  Interval of float<s>
     |  CreatePathIfNotExists of bool
     |  AfterSuccess of (Message -> FSScript)
     |  AfterError   of (Message -> FSScript)
     |  ConcurrentTasks of int


type Options = {
     Interval           : float<s>
     CreateIfNotExists  : bool
     AfterSuccess       : (Message -> FSScript)
     AfterError         : (Message -> FSScript)
     ConcurrentTasks    : int
    }


/// Contains "File" configuration
type Properties = {
        Id           : Guid
        Path         : string
        Options      : Options
    }
    with
    /// Converts a list of FileOption into an Option record. Each given option overrides a default, so the defaults are coded here.
    static member convertOptions options =
        let subDir source sub =
            let filename = Path.GetFileName(source)
            let folder = Path.Combine(Path.GetDirectoryName(source), sub) 
            (source, folder, Path.Combine(folder, filename))
            
        let defaultOptions = {
            Interval = 10.0<s>; 
            CreateIfNotExists = false
            AfterSuccess = fun _ -> FSScript.Empty
            AfterError = fun _ -> FSScript.Empty
            ConcurrentTasks = 0
        }
        options 
        |> List.fold (fun state option ->
            match option with
            |   Interval(i)              -> {state with Interval = i}
            |   CreatePathIfNotExists(b) -> {state with CreateIfNotExists = b}
            |   AfterSuccess(func)       -> {state with AfterSuccess = func}
            |   AfterError(func)         -> {state with AfterError = func}
            |   ConcurrentTasks(amount)  -> 
                if amount > 0 then
                    {state with ConcurrentTasks = amount}
                else
                    raise <| FileComponentException "ERROR: ConcurrentTasks must be larger than 0"
        ) defaultOptions

    static member Create path options = 
        let convertedOptions = Properties.convertOptions options
        {Id = Guid.NewGuid(); Path = path ; Options = convertedOptions}


/// Contains "File" state
type State = {
        ProducerHook : ProducerMessageHook option
        Timer           : Timer
        RunningState    : ProducerState
        Cancellation    : CancellationTokenSource
        EngineServices  : IEngineServices option
        TaskPool        : RestrictedResourcePool
    }
    with
    static member Create convertedOptions = 
        let timer = new Timer(Internal.secsToMsFloat <| convertedOptions.Interval)
        {ProducerHook = None; Timer = timer; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None; TaskPool = RestrictedResourcePool.Create <| convertedOptions.ConcurrentTasks}

    member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
    member this.SetEngineServices services = {this with EngineServices = services}


#nowarn "0050"  // warning that implementation of some interfaces are invisible because absent in signature. But that's exactly what we want.
type File(props : Properties, state: State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger("debug"); 

    /// Change this instance's state
    let changeState (newState:State) = File(props, newState)

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
        let configUri = new Uri(props.Path)

        if not(Directory.Exists(configUri.AbsolutePath)) then
            if props.Options.CreateIfNotExists then
                try
                    Directory.CreateDirectory(configUri.AbsolutePath) |> ignore
                with
                | e -> raise (FileComponentException(sprintf "Cannot create directory '%s'\n%A" configUri.AbsolutePath e))

            else
                raise (FileComponentException(sprintf "Cannot start File listener: path '%s' does not exists. Create the path, or configure File listener with 'CreatePathIfNotExists' setting." configUri.AbsolutePath))

        /// Retrieve Xml content
        let getXmlContent (f:FileInfo) = 
            if f.Extension.ToLower() = ".xml" then 
                use stream = f.OpenText()
                stream.ReadToEnd()
            else
                ""

        /// Process a file
        let processFile fileInfo sendToRoute = 
            //#region fsRun action // try .. with
            /// Run filesystem commands, catch any exceptions
            let fsRun action =
                try
                    FSScript.Run <| action
                with
                |  e -> 
                    printfn "Exception! %A" e
                    logger.Error(e)
            //#endregion

            let content = getXmlContent fileInfo
            let message = Message.Empty.SetBody content
            let message = message.SetProducerHeader <| FileMessageHeader.Create fileInfo
            try
                sendToRoute message
                fsRun <| props.Options.AfterSuccess message
            with
            |  e -> 
                fsRun <| props.Options.AfterError message

        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop() = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            let sendToRoute = state.ProducerHook.Value
            try
                Directory.GetFiles(configUri.LocalPath)  |> List.ofArray
                    |> List.map(fun filename -> new FileInfo(filename))
                    |> List.sortBy (fun fileInfo -> fileInfo.CreationTimeUtc)
                    |> List.iter(fun fileInfo -> state.TaskPool.PooledAction(fun () -> processFile fileInfo sendToRoute))
            with
            |   e -> printfn "%A" e; logger.Error e

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

