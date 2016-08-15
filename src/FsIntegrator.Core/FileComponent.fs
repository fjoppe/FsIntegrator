namespace FsIntegrator

open System
open System.IO
open System.Threading
open System.Timers
open FSharp.Data.UnitSystems.SI.UnitSymbols
open FSharpx.Control
open NLog
open FsIntegrator.Routing.Types
open FsIntegrator.MessageOperations
open FsIntegrator.Utility

exception FileComponentException of string

type FileMessageHeader = {
        FileInfo : FileInfo
    }
    with
        static member Create fileInfo =
            { FileInfo = fileInfo}

type FileOption =
     |  InitialDelay of float<s>
     |  Interval of float<s>
     |  CreatePathIfNotExists of bool
     |  AfterSuccess of (Message -> FSScript)
     |  AfterError   of (Message -> FSScript)
     |  ConcurrentTasks of int
     |  EndpointFailureStrategy of EndpointFailureStrategy


module FileInternal =

    type Options = {
         InitialDelay       : float<s>
         Interval           : float<s>
         CreateIfNotExists  : bool
         AfterSuccess       : (Message -> FSScript)
         AfterError         : (Message -> FSScript)
         ConcurrentTasks    : int
         EndpointFailureStrategy : EndpointFailureStrategy
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
                InitialDelay = 0.0<s>;
                Interval = 10.0<s>; 
                CreateIfNotExists = false
                AfterSuccess = fun _ -> FSScript.Empty
                AfterError = fun _ -> FSScript.Empty
                ConcurrentTasks = 0
                EndpointFailureStrategy = EndpointFailureStrategy.StopImmediately
            }
            options 
            |> List.fold (fun state option ->
                match option with
                |   InitialDelay(wt)         -> 
                    let wait = Utility.secsToMsFloat wt
                    if wait < 0.0 then raise(ValidationException "ERROR: InitialDelay cannot be less than 0.0")
                    {state with InitialDelay = wt}
                |   Interval(wt)             ->
                    let wait = Utility.secsToMsFloat wt
                    if wait < 0.01 then raise(ValidationException "ERROR: Interval cannot be less than 0.01")
                    {state with Interval = wt}
                |   CreatePathIfNotExists(b) -> {state with CreateIfNotExists = b}
                |   AfterSuccess(func)       -> {state with AfterSuccess = func}
                |   AfterError(func)         -> {state with AfterError = func}
                |   ConcurrentTasks(amount)  -> 
                    if amount <= 0 then raise(ValidationException "ERROR: ConcurrentTasks must be larger than 0")
                    {state with ConcurrentTasks = amount}                          
                |  EndpointFailureStrategy(strategy) ->
                    strategy.Validate()
                    {state with EndpointFailureStrategy = strategy}
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
            let timer = new Timer(Utility.secsToMsFloat <| convertedOptions.Interval)
            {ProducerHook = None; Timer = timer; RunningState = Stopped; Cancellation = new CancellationTokenSource(); EngineServices = None; TaskPool = RestrictedResourcePool.Create <| convertedOptions.ConcurrentTasks}

        member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
        member this.SetEngineServices services = {this with EngineServices = services}


    type Operation =
        |   SetProducerHook of ProducerMessageHook * ActionAsyncResponse
        |   SetEngineServices of IEngineServices   * ActionAsyncResponse
        |   ChangeRunningState of ProducerState  * (State -> State)  * ActionAsyncResponse
        |   GetRunningState of FunctionsAsyncResponse<ProducerState>
        |   GetEngineServices of FunctionsAsyncResponse<IEngineServices>


open FileInternal
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
            logger.Trace(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    /// Stop File polling
    let stopFilePolling state = 
        state.Timer.Stop()
        state.Cancellation.Cancel()
        logger.Debug(sprintf "Stopped FileListener for path: '%s'" (getPath None))
        {state with Cancellation = new CancellationTokenSource()}   // the CancellationToken is not reusable, so we make this for the next "start"

    /// State File polling
    let startFilePolling state = 
        let configUri = new Uri(getPath None)

        if not(Directory.Exists(configUri.AbsolutePath)) then
            if props.Options.CreateIfNotExists then
                try
                    Directory.CreateDirectory(configUri.AbsolutePath) |> ignore
                    logger.Debug(sprintf "Created directory: '%s'" configUri.AbsolutePath)
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
                |  e -> logger.Error e
            //#endregion

            let content = getXmlContent fileInfo
            let message = Message.Empty.SetBody content
            let message = message.SetProducerHeader <| FileMessageHeader.Create fileInfo
            try
                sendToRoute message |> ignore
                fsRun <| props.Options.AfterSuccess message
                logger.Debug(sprintf "File processing OK, called AfterSuccess() for '%s'" fileInfo.FullName)
            with
            |  e -> 
                logger.Error(sprintf "Error: %A" e)
                fsRun <| props.Options.AfterError message
                logger.Debug(sprintf "File processing ERROR, called AfterError() for '%s'" fileInfo.FullName)

        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop retryCount = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            let sendToRoute = state.ProducerHook.Value
            try
                Directory.GetFiles(configUri.LocalPath)  |> List.ofArray
                    |> List.map(fun filename -> new FileInfo(filename))
                    |> List.sortBy (fun fileInfo -> fileInfo.CreationTimeUtc)
                    |> List.iter(fun fileInfo -> 
                        logger.Debug(sprintf "Received file: '%s'" fileInfo.FullName)
                        state.TaskPool.PooledAction(fun () -> processFile fileInfo sendToRoute))
                return! loop(None)
            with
            |   e -> 
                logger.Error e
                let sleepAndLoop wt s =
                    let waitInMs = wt |> Utility.secsToMsFloat
                    Async.Sleep <| int(waitInMs) |> Async.RunSynchronously
                    loop(Some s)
                match props.Options.EndpointFailureStrategy with
                |  EndpointFailureStrategy.WaitAndRetryInfinite wt -> 
                    logger.Debug (sprintf "Retrying... EndpointFailure strategy is set to: WaitAndRetryInfinite(%A)" wt)
                    return! (sleepAndLoop wt 0)
                |  EndpointFailureStrategy.WaitAndRetryCountDownBeforeStop(wt, cnt) ->
                    match retryCount with
                    |   Some(x) -> 
                        if(x < cnt) then 
                            logger.Debug (sprintf "Retrying... EndpointFailure strategy is set to: WaitAndRetryCountDownBeforeStop(%A, %d); retry: %d of %d" wt cnt x cnt)
                            return! (sleepAndLoop wt (x+1))
                        else 
                            logger.Debug (sprintf "Retrying... EndpointFailure strategy is set to: WaitAndRetryCountDownBeforeStop(%A, %d); stopping.." wt cnt)
                            (this :> IProducerDriver).Stop()
                    |   None    -> 
                        logger.Debug (sprintf "Retrying... EndpointFailure strategy is set to: WaitAndRetryCountDownBeforeStop(%A, %d); retry: %d of %d" wt cnt 1 cnt)
                        return! (sleepAndLoop wt 1)
                |  StopImmediately ->
                    logger.Debug "Stopping... EndpointFailure strategy is set to: StopImmediately"
                    (this :> IProducerDriver).Stop()
        }

        if props.Options.InitialDelay > 0.0<s> then
            logger.Debug (sprintf "InitialDelay is set to %A, waiting..." props.Options.InitialDelay)
            let waitInMs = int(props.Options.InitialDelay |> Utility.secsToMsFloat)
            Async.Sleep <| waitInMs |> Async.RunSynchronously

        state.Timer.Start()
        Async.Start(loop(None), cancellationToken = state.Cancellation.Token)
        logger.Debug(sprintf "Started FileListener for path: '%s'" (getPath None))
        state


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
                    logger.Trace "Waiting for message.."
                    let! command = inbox.Receive()
                    try
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
            logger.Trace("Validate()")
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
                logger.Trace(sprintf "Is valid: %b" <| not(invalid))
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
            logger.Debug(sprintf "Write message to path: '%s'" path)
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
                logger.Trace("GetConsumerHook.get()")
                this.Consume

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore
