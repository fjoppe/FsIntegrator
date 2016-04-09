namespace Camel.FileTransfer

open System
open System.IO
open System.Net.FtpClient
open System.Text
open System.Threading
open System.Timers
open Camel.Core
open Camel.Core.EngineParts
open Camel.Core.General
open FSharp.Data.UnitSystems.SI.UnitSymbols
open RemoteFileSystem
open NLog
open Camel.Utility


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


type Properties = {
        Id          : Guid
        Path        : string
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


#nowarn "0050"  // warning that implementation of "RouteEngine.IProducer'" is invisible because absent in signature. But that's exactly what we want.
type Ftp(props : Properties, state : State) as this = 
    inherit ProducerConsumer()

    let logger = LogManager.GetLogger("debug"); 

    /// Change this instance's state
    let changeState (newState:State) = Ftp(props, newState)

    /// Do "action" when there is a ProducerHook, else raise exception
    let witProducerHookOrFail action =
        if state.ProducerHook.IsSome then action
        else raise (MissingMessageHook(sprintf "File with path '%s' has no producer hook." props.Path))

    let getFtpClient() =
        let connectUri = match props.Options.Credentials with
                         |  None        -> Uri(sprintf "ftp://%s" props.Connection)
                         |  Some(creds) -> Uri(sprintf "ftp://%s:%s@%s" creds.Username creds.Password props.Connection)

        let client = FtpClient.Connect(connectUri)
        client

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
                    printfn "Exception! %A" e
                    logger.Error(e)
            //#endregion

            let content = getXmlContent fileInfo.FullPath
            let message = Message.Empty.SetBody content
            let message = message.SetProducerHeader <|  FtpMessageHeader.Create fileInfo
            try
                sendToRoute message
                fsRun <| props.Options.AfterSuccess message
            with
            |  e -> 
                fsRun <| props.Options.AfterError message

        /// Poll a target for files and process them. The polling stops when busy with a batch of files.
        let rec loop() = async {
            let! waitForElapsed = Async.AwaitEvent state.Timer.Elapsed

            match state.ProducerHook with
            | Some(sendToRoute) ->
                try
                    client.GetListing(props.Path) |> List.ofArray
                    |> List.map(
                        fun ftpFile ->
                            let name, path = ftpFile.Name, ftpFile.FullName
                            let path = path.Substring(0, path.Length-name.Length)
                            RemoteFile.Create name path ftpFile.FullName (ftpFile.Size) (ftpFile.Created) (ftpFile.Modified)
                        )
                    |> List.sortBy (fun fileInfo -> fileInfo.Created)
                    |> List.iter(fun fileInfo -> state.TaskPool.PooledAction(processFile fileInfo sendToRoute))
                with
                |   e -> printfn "%A" e
                         logger.Error e

            | None -> Async.Sleep (WaitForHook*1000) |> Async.RunSynchronously
            return! loop()
        }
        state.Timer.Start()
        Async.Start(loop(), cancellationToken = state.Cancellation.Token)
        { state with FtpClient = client }


    member this.stopFilePolling() = 
        state.Timer.Stop()
        state.Cancellation.Cancel()
        state.FtpClient.Disconnect()
        {state with Cancellation = new CancellationTokenSource()}   // the CancellationToken is not reusable, so we make this for the next "start"


    new(path, connection, optionList) =
        let options = Properties.Create path connection optionList
        let fileState = State.Create <| options.Options
        Ftp(options, fileState)


    //  =============================================== Producer ===============================================
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
                let ftpListenerList = engine.producerList<Ftp>()
                let invalid = 
                    ftpListenerList |>  List.tryPick(fun ftp -> 
                        let refId = ftp.Properties.Id
                        let refPath = ftp.Properties.Path
                        let foundList = 
                            ftpListenerList 
                            |> List.filter(fun i -> i.Properties.Id <> refId)
                            |> List.filter(fun i -> i.Properties.Path = refPath)
                        if foundList.Length = 0 then None
                        else Some(foundList.Head)
                    )
                invalid.IsNone


    //  ===============================================  Consumer  ===============================================
    member private this.writeFile (message:Message) =        
        let bufferToWrite = System.Text.Encoding.UTF8.GetBytes(message.Body)
        use targetStream = state.FtpClient.OpenWrite(props.Path) :?> FtpDataStream
        targetStream.Write(bufferToWrite, 0, bufferToWrite.Length)
        let reply = targetStream.Close()
        if not(reply.Success) then
            raise(FtpComponentException(reply.ErrorMessage))

    member private this.Consume (message:Message) =
        this.writeFile message

    interface ``Provide a Consumer Driver`` with
        override this.ConsumerDriver 
            with get() = 
                let client = getFtpClient()
                client.Connect()
                Ftp(props, {state with FtpClient = client}) :> IConsumerDriver

    interface IConsumerDriver with
        member self.GetConsumerHook with get() = this.Consume

