namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileTransfer
open Camel.FileTransfer.RemoteFileSystem
open System.IO

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
                let (source, folder, targetPath) = subDir (fi.FileInfo) ".camel"
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

    let defaultProducerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault);ConcurrentTasks(1)]


module Producers =
    type From = struct end
    type From with
        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member Ftp(path, connection) = Ftp(path, connection, FtpProducerDefaults.defaultProducerOptions)

        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member Ftp(path, connection, options) = Ftp(path, connection, FtpProducerDefaults.defaultProducerOptions @ options)

