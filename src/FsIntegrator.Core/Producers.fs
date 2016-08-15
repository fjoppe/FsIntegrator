namespace FsIntegrator

open System.IO
open FsIntegrator.Core
open FSharp.Data.UnitSystems.SI.UnitSymbols

module FileProducerDefaults =
    let subDir source sub =
        let filename = Path.GetFileName(source)
        let folder = Path.Combine(Path.GetDirectoryName(source), sub) 
        (source, folder, Path.Combine(folder, filename))

    let afterSuccessDefault = 
        fun (originalMessage : Message) ->
            let fileinfo = originalMessage.Headers.GetProducer<FileMessageHeader>()
            match fileinfo with
            |   Some(fi)  ->
                let (source, folder, targetPath) = subDir (fi.FileInfo.FullName) ".success"
                fs { 
                        createdir folder
                        move source targetPath
                    }
            |   None      -> failwith "No file headers found! If you see this error, then it is a framework issue."

    let afterErrorDefault  =
        fun (originalMessage : Message) ->
            let fileinfo = originalMessage.Headers.GetProducer<FileMessageHeader>()
            match fileinfo with
            |   Some(fi)  ->
                let (source, folder, targetPath) = subDir (fi.FileInfo.FullName) ".error"
                fs { 
                        createdir folder
                        move source targetPath
                    }
            |   None      -> failwith "No file headers found! If you see this error, then it is a framework issue."

    let endpointFailureStrategy = FileOption.EndpointFailureStrategy(WaitAndRetryInfinite(5.0<s>))

    let defaultProducerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault); ConcurrentTasks(1); endpointFailureStrategy]

module SubRouteProducerDefaults =

    let defaultProducerOptions = []

type From = struct end
type From with
    /// Create a File-listener Producer, which listens to a folder on the local filesystem
    static member File(path : string) = File(path, FileProducerDefaults.defaultProducerOptions)

    /// Create a File-listener Producer, which listens to a folder on the local filesystem
    static member File(path : string, options) = File(path, FileProducerDefaults.defaultProducerOptions @ options)
 
    /// Create a Subroute, which is identified by the specified name
    static member SubRoute(name : string) = SubRoute(name, SubRouteProducerDefaults.defaultProducerOptions)

    /// Create a Subroute, which is identified by the specified name
    static member SubRoute(name : string, options) = SubRoute(name, SubRouteProducerDefaults.defaultProducerOptions @ options)

