namespace FsIntegrator.FileHandling

open System
open System.IO
open System.Threading
open System.Timers
open FSharp.Data.UnitSystems.SI.UnitSymbols
open FSharpx.Control
open NLog
open FsIntegrator.Core
open FsIntegrator.Core.EngineParts
open FsIntegrator.Core.General
open FsIntegrator.Core.MessageOperations
open FsIntegrator.Utility
open FsIntegrator.FileHandling.FileSystem

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


type PathType =
    |   Fixed       of string
    |   Evaluate    of StringMacro

/// Contains "File" configuration
type Properties = {
        Id           : Guid
        Path         : PathType
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
        ProducerHook    : ProducerMessageHook option
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


type Operation =
    |   SetProducerHook of ProducerMessageHook * ActionAsyncResponse
    |   SetEngineServices of IEngineServices   * ActionAsyncResponse
    |   ChangeRunningState of ProducerState  * (State -> State)  * ActionAsyncResponse
    |   GetRunningState of FunctionsAsyncResponse<ProducerState>
    |   GetEngineServices of FunctionsAsyncResponse<IEngineServices>

#nowarn "0050"  // warning that implementation of some interfaces are invisible because absent in signature. But that's exactly what we want.
type File(props : Properties, initialState: State) as this = 

    let logger = LogManager.GetLogger(this.GetType().FullName); 

    let getPath (message: Message option) =
        match props.Path with
        |   Fixed str   -> str
        |   Evaluate strmacro ->
            match message with
            |   Some    (msg) -> strmacro.Substitute(msg)
            |   None          -> raise <| FileComponentException("Cannot evaluate StringMacro without message")

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "File with path '%s' has no producer hook." (getPath None)))

    /// Change the running state of this instance
    let changeRunningState state targetState (action: unit -> State) = 
        if state.RunningState = targetState then state
        else 
            let newState = witProducerHookOrFail state action
            logger.Debug(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    /// State File polling
    let startFilePolling state = 
        let configUri = new Uri(getPath None)

        if not(Directory.Exists(configUri.AbsolutePath)) then
            if props.Options.CreateIfNotExists then
                try
                    Directory.CreateDirectory(configUri.AbsolutePath) |> ignore
                    logger.Debug(sprintf "Created directory: %s" configUri.AbsolutePath)
                with
                | e -> 
                    let msg = sprintf "Cannot create directory '%s'\n%A" configUri.AbsolutePath e
                    logger.Error msg
                    raise (FileComponentException(msg))

            else
                let msg =sprintf "Cannot start File listener: path '%s' does not exists. Create the path, or configure File listener with 'CreatePathIfNotExists' setting." configUri.AbsolutePath
                logger.Error msg
                raise (FileComponentException(msg))

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
                sendToRoute message |> ignore
                fsRun <| props.Options.AfterSuccess message
                logger.Debug(sprintf "File OK: Processed AfterSuccess()")
            with
            |  e -> 
                logger.Error(sprintf "Error: %A" e)
                fsRun <| props.Options.AfterError message
                logger.Debug(sprintf "File Error: Processed AfterError")

        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop() = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            let sendToRoute = state.ProducerHook.Value
            try
                Directory.GetFiles(configUri.LocalPath)  |> List.ofArray
                    |> List.map(fun filename -> new FileInfo(filename))
                    |> List.sortBy (fun fileInfo -> fileInfo.CreationTimeUtc)
                    |> List.iter(fun fileInfo -> 
                        logger.Debug(sprintf "Send file to route: %s" fileInfo.FullName)
                        state.TaskPool.PooledAction(fun () -> processFile fileInfo sendToRoute))
            with
            |   e -> printfn "%A" e; logger.Error e

            return! loop()
        }
        state.Timer.Start()
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)
        logger.Debug(sprintf "Started FileListener for path: %s" (getPath None))
        state


    let stopFilePolling state = 
        state.Timer.Stop()
        state.Cancellation.Cancel()
        logger.Debug(sprintf "Stopped FileListener for path: %s" (getPath None))
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
                    logger.Debug "Waiting for message.."
                    let! command = inbox.Receive()
                    try
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
                        |   GetEngineServices replychannel ->
                            logger.Debug "GetEngineServices"
                            match state.EngineServices with
                            |   None       -> replychannel.Reply <| ERROR(FileComponentException("This consumer is not connected with a route-engine"))
                            |   Some value -> replychannel.Reply <| Response(value)
                            return! loop state
                    with
                    |   e -> logger.Error(sprintf "Uncaught exception: %A" e) 
                    return! loop state
                }
            loop initialState)
        newAgent.Start()
        newAgent

    member private this.Properties with get() = props
    member private this.State with get() = initialState

    new(path : PathType, optionList : FileOption list) =
        let options = Properties.Create path optionList
        let fileState = State.Create <| options.Options
        File(options, fileState)

    new(path : string, optionList : FileOption list) =
        File((Fixed path), optionList)

    new(path : StringMacro, optionList : FileOption list) =
        File((Evaluate path), optionList)


    //  =============================================== Producer ===============================================
    interface IProducer
    interface ``Provide a Producer Driver`` with
        override this.ProducerDriver with get() = this :> IProducerDriver
    interface IProducerDriver with
        member this.Start() = agent.PostAndReply(fun replychannel -> ChangeRunningState(Running, startFilePolling, replychannel)) |> ignore
        member this.Stop() = agent.PostAndReply(fun replychannel -> ChangeRunningState(Stopped, stopFilePolling, replychannel)) |> ignore
        member this.SetProducerHook hook = agent.PostAndReply(fun replychannel -> SetProducerHook(hook, replychannel)) |> ignore

        member this.RunningState 
            with get () = 
                let result = agent.PostAndReply(fun replychannel -> GetRunningState(replychannel))
                result.GetResponseOrRaise()

        member this.Validate() =
            logger.Debug("Validate()")
            let servicesResponse = agent.PostAndReply(fun replychannel -> GetEngineServices(replychannel))
            let services = servicesResponse.GetResponseOrRaise()
            let fileListenerList = services.producerList<File>()
            match props.Path with
            |   Fixed propertiesPath ->
                let invalid = 
                    fileListenerList 
                    |> List.filter( fun e -> e.Properties.Id <> props.Id)
                    |> List.exists(fun fp ->
                        let refId = fp.Properties.Id
                        match fp.Properties.Path with
                        | Fixed refPath -> (refPath = propertiesPath)
                        | _ -> false)
                logger.Debug(sprintf "Is valid: %b" <| not(invalid))
                not(invalid)
            |   _ -> true



    //  ===============================================  Consumer  ===============================================
    interface IConsumer

    member private this.writeFile (path:string) (message:Message) =
        let configUri = new Uri(path)
        File.WriteAllText(configUri.AbsolutePath, message.Body)

    member private this.Consume (message:Message) =
        try
            let path = getPath (Some message)
            logger.Debug(sprintf "Write message to path: %s" path)
            this.writeFile path message
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
                logger.Debug("GetConsumerHook.get()")
                this.Consume

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore
