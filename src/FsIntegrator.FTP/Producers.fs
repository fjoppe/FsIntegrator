namespace FsIntegrator

open FsIntegrator
open FsIntegrator.Core
open System.IO
open FSharp.Data.UnitSystems.SI.UnitSymbols


    module FtpProducerDefaults =
        let subDir source sub =
            let filename = source.Filename
            let folder = sprintf "%s%s" source.Folder sub
            let targetPath = sprintf "%s/%s" folder filename
            (source.FullPath, folder , targetPath)

        let afterSuccessDefault = 
            fun (originalMessage : Message) ->
                let fileinfo = originalMessage.Headers.GetProducer<FtpMessageHeader>()
                match fileinfo with
                |   Some(fi)  ->
                    let (source, folder, targetPath) = subDir (fi.FileInfo) ".success"
                    ftp { 
                            createdir folder
                            move source targetPath
                        }
                |   None      -> failwith "No file headers found! If you see this error, then it is a framework issue."

        let afterErrorDefault  =
            fun (originalMessage : Message) ->
                let fileinfo = originalMessage.Headers.GetProducer<FtpMessageHeader>()
                match fileinfo with
                |   Some(fi)  ->
                    let (source, folder, targetPath) = subDir (fi.FileInfo) ".error"
                    ftp { 
                            createdir folder
                            move source targetPath
                        }
                |   None      -> failwith "No file headers found! If you see this error, then it is a framework issue."

        let transferMode = FtpOption.TransferMode(TransferMode.Active)

        let endpointFailureStrategy = FtpOption.EndpointFailureStrategy(WaitAndRetryInfinite(5.0<s>))

        let defaultProducerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault);ConcurrentTasks(1);transferMode;endpointFailureStrategy]


type From = struct end
type From with
    /// Create a File-listener Producer, which listens to a folder on the local filesystem
    static member Ftp(path : string, connection) = Ftp(path, connection, FtpProducerDefaults.defaultProducerOptions)

    /// Create a File-listener Producer, which listens to a folder on the local filesystem
    static member Ftp(path : string, connection, options) = Ftp(path, connection, FtpProducerDefaults.defaultProducerOptions @ options)

