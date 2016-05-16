namespace FsIntegrator.FileTransfer

open System
open System.IO
open System.Net
open System.Text
open System.Threading
open System.Timers
open System.Net.FtpClient
open FSharp.Data.UnitSystems.SI.UnitSymbols
open NLog
open FsIntegrator.Core
open FsIntegrator.Core.EngineParts
open FsIntegrator.Core.General
open FsIntegrator.Core.MessageOperations
open FsIntegrator.FileTransfer.RemoteFileSystem
open FsIntegrator.Utility


exception FtpComponentException of string


type RemoteFile = {
        Filename : string
        Folder   : string
        FullPath : string
        Size     : int64
        Created  : DateTime
        Modified : DateTime
    }
    with
        static member Create name folder fullpath size created modified =
            {Filename = name; Folder = folder; FullPath = fullpath; Size = size; Created = created; Modified = modified}


type FtpMessageHeader = {
        FileInfo : RemoteFile
    }
    with
        static member Create fileInfo =
            { FileInfo = fileInfo}


module Internal =
    let secsToMsFloat (s:float<s>) = (s * 1000.0) / 1.0<s>


type FtpOption =
    |   Interval of float<s>
    |   Credentials of Credentials
    |   AfterSuccess of (Message -> FtpScript)
    |   AfterError   of (Message -> FtpScript)
    |   ConcurrentTasks of int


type Options = {
        Interval        : float<s>
        Credentials     : Credentials option
        AfterSuccess    : (Message -> FtpScript)
        AfterError      : (Message -> FtpScript)
        ConcurrentTasks : int
    }

type PathType =
    |   Fixed       of string
    |   Evaluate    of StringMacro


type Properties = {
        Id          : Guid
        Path        : PathType
        Connection  : string
        Options     : Options
    }
    with
    static member convertOptions options =
        let defaultOptions = {
            Interval = 10.0<s> 
            Credentials = None
            AfterSuccess = fun _ -> FtpScript.Empty
            AfterError = fun _ -> FtpScript.Empty
            ConcurrentTasks = 0
        }
        options 
        |> List.fold (fun state option ->
            match option with
            |   Interval(i)         -> {state with Interval = i}
            |   Credentials(c)      -> {state with Credentials = Some(c)}
            |   AfterSuccess(func)  -> {state with AfterSuccess = func}
            |   AfterError(func)    -> {state with AfterError = func}
            |   ConcurrentTasks(amount)  -> 
                if amount > 0 then
                    {state with ConcurrentTasks = amount}
                else
                    raise <| FtpComponentException "ERROR: ConcurrentTasks must be larger than 0"
        ) defaultOptions

    static member Create path connection options = 
        let convertedOptions = Properties.convertOptions options
        {Id = Guid.NewGuid(); Path = path; Connection = connection; Options = convertedOptions}


type State = {
        ProducerHook    : ProducerMessageHook option
        RunningState    : ProducerState
        Cancellation    : CancellationTokenSource
        FtpClient       : FtpClient
        Timer           : Timer
        EngineServices  : IEngineServices option
        TaskPool        : RestrictedResourcePool
    }
    with
    static member Create convertedOptions = 
        let timer = new Timer(Internal.secsToMsFloat <| convertedOptions.Interval)
        {ProducerHook = None; Timer = timer; RunningState = Stopped; Cancellation = new CancellationTokenSource(); FtpClient = new FtpClient(); EngineServices = None; TaskPool = RestrictedResourcePool.Create <| convertedOptions.ConcurrentTasks}


    member this.SetProducerHook hook = {this with ProducerHook = Some(hook)}
    member this.SetEngineServices services = {this with EngineServices = services}

type Operation =
    |   SetProducerHook of ProducerMessageHook * ActionAsyncResponse
    |   SetEngineServices of IEngineServices   * ActionAsyncResponse
    |   ChangeRunningState of ProducerState  * (State -> State)  * ActionAsyncResponse
    |   GetRunningState of FunctionsAsyncResponse<ProducerState>
    |   GetEngineServices of FunctionsAsyncResponse<IEngineServices>


#nowarn "0050"  // warning that implementation of "RouteEngine.IProducer'" is invisible because absent in signature. But that's exactly what we want.
type Ftp(props : Properties, initialState : State) as this = 
    
    let logger = LogManager.GetLogger(this.GetType().FullName); 

    let getPath (message: Message option) =
        match props.Path with
        |   Fixed str   -> str
        |   Evaluate strmacro ->
            match message with
            |   Some    (msg) -> strmacro.Substitute(msg)
            |   None          -> raise <| FtpComponentException("Cannot evaluate StringMacro without message")

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail state action =
        if state.ProducerHook.IsSome then action()
        else raise (MissingMessageHook(sprintf "File with path '%s' has no producer hook." (getPath None)))

    let getHostUri() = Uri(sprintf "ftp://%s" props.Connection)

    let getFtpClient() =
        logger.Trace("getFtpClient()")
        let connectUri = getHostUri()
        logger.Trace(sprintf "Connection: '%A'" connectUri)

        let client = new FtpClient()
        client.Host <- connectUri.Host

        match props.Options.Credentials with
        |  None        ->
            logger.Trace("No FTP Credentials")
            ()
        |  Some(creds) -> 
            logger.Trace("Applying FTP Credentials")
            client.Credentials <- new NetworkCredential(creds.Username, creds.Password)
        client

    /// Change the running state of this instance
    let changeRunningState state targetState (action: unit -> State) = 
        if state.RunningState = targetState then state
        else 
            let newState = witProducerHookOrFail state action
            logger.Trace(sprintf "Changing running state to: %A" targetState)
            {newState with RunningState = targetState}

    /// State File polling
    let startFilePolling state = 
        let client = getFtpClient()
        client.Connect()

       /// Retrieve Xml content
        let getXmlContent (filename:string) =
            let f = FileInfo(filename)
            if f.Extension.ToLower() = ".xml" then 
                use memoryStream = new MemoryStream()
                use dataStream = client.OpenRead(filename, FtpDataType.Binary) :?> FtpDataStream
                dataStream.CopyTo(memoryStream)
                memoryStream.Position <- 0L
                use stringReader = new StreamReader(memoryStream)
                let fileContent = stringReader.ReadToEnd()
                stringReader.Close()
                memoryStream.Close()
                let reply = dataStream.Close()
                if not(reply.Success) then
                    raise(FtpComponentException(reply.ErrorMessage))
                else
                    fileContent
            else
                ""

        /// Process a file
        let processFile fileInfo sendToRoute = 
            //#region fsRun action // try .. with
            /// Run filesystem commands, catch any exceptions
            let fsRun action =
                try
                    FtpScript.Run client action
                with
                |  e -> 
                    logger.Error(e)
            //#endregion

            let content = getXmlContent fileInfo.FullPath
            let message = Message.Empty.SetBody content
            let message = message.SetProducerHeader <|  FtpMessageHeader.Create fileInfo
            try
                sendToRoute message |> ignore
                fsRun <| props.Options.AfterSuccess message
            with
            |  e -> 
                fsRun <| props.Options.AfterError message

        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop() = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            let sendToRoute = state.ProducerHook .Value
            try
                client.GetListing(getPath None) |> List.ofArray
                |> List.map(
                    fun ftpFile ->
                        let name, path = ftpFile.Name, ftpFile.FullName
                        let path = path.Substring(0, path.Length-name.Length)
                        RemoteFile.Create name path ftpFile.FullName (ftpFile.Size) (ftpFile.Created) (ftpFile.Modified)
                    )
                |> List.sortBy (fun fileInfo -> fileInfo.Created)
                |> List.iter(fun fileInfo -> state.TaskPool.PooledAction(fun () -> processFile fileInfo sendToRoute))
            with
            |   e -> logger.Error e

            return! loop()
        }
        state.Timer.Start()
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)
        logger.Debug(sprintf "Started FTP Listener for path: %s" (getPath None))
        { state with FtpClient = client }


    let stopFilePolling state = 
        state.Timer.Stop()
        state.Cancellation.Cancel()
        state.FtpClient.Disconnect()
        logger.Debug(sprintf "Stopped FTP Listener for path: %s" (getPath None))
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
                            |   None       -> replychannel.Reply <| ERROR(FtpComponentException("This consumer is not connected with a route-engine"))
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


    new(path, connection, optionList : FtpOption list) =
        let options = Properties.Create path connection optionList
        let fileState = State.Create <| options.Options
        Ftp(options, fileState)

    new(path : string, connection : string, optionList: FtpOption list) =
        Ftp((Fixed path), connection, optionList)

    new(path : StringMacro, connection : string, optionList: FtpOption list) =
        Ftp((Evaluate path), connection, optionList)

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
            let ftpListenerList = services.producerList<Ftp>()

            match props.Path with
            |   Fixed propertiesPath ->
                let invalid =
                    ftpListenerList
                    |>  List.filter(fun e -> e.Properties.Id <> props.Id)
                    |>  List.exists(fun fp ->
                        match fp.Properties.Path with
                        |   Fixed refPath -> propertiesPath = refPath
                        |   _ -> false
                    )
                logger.Debug(sprintf "Is valid: %b" <| not(invalid))
                not(invalid)
            |   _ -> true


    //  ===============================================  Consumer  ===============================================
    interface IConsumer

    member private this.writeFile (message:Message) =      
        try
            let path = getPath (Some message)
            logger.Debug(sprintf "Write to: '%A/%s'" (getHostUri()) path)
            let bufferToWrite = System.Text.Encoding.UTF8.GetBytes(message.Body)
            use targetStream = initialState.FtpClient.OpenWrite(path) :?> FtpDataStream
            targetStream.Write(bufferToWrite, 0, bufferToWrite.Length)
            let reply = targetStream.Close()
            if not(reply.Success) then
                raise(FtpComponentException(reply.ErrorMessage))
        with
        |   e ->
            logger.Error(sprintf "Error: %A" e)
            reraise()

    member private this.Consume (message:Message) =
        let client = initialState.FtpClient
        if not(client .IsConnected) then 
            logger.Trace("Not connected yet, now making connection...")
            client.Connect()
            let connectUri = getHostUri()
            client.SetWorkingDirectory(client.GetWorkingDirectory() + connectUri.LocalPath)
            logger.Debug(sprintf "Connected to: '%A', Current workdir: '%s'" (connectUri) (client.GetWorkingDirectory()))
        this.writeFile message
        message

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver 
            with get() = 
                let client = getFtpClient()
                Ftp(props, {initialState with FtpClient = client}) :> IConsumerDriver

    interface IConsumerDriver with
        member self.GetConsumerHook 
            with get() = 
                logger.Trace("GetConsumerHook.get()")
                this.Consume

    interface IRegisterEngine with
        member this.Register services = agent.PostAndReply(fun replychannel -> SetEngineServices(services, replychannel)) |> ignore

