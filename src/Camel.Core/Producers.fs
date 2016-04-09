namespace Camel

open System.IO
open Camel.Core.General
open Camel.FileHandling
open Camel.FileHandling.FileSystem

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
                let (source, folder, targetPath) = subDir (fi.FileInfo.FullName) ".camel"
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

    let defaultFsScripts = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault); ConcurrentTasks(1)]

module Producers =
    type From = struct end
    type From with
        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member File(path)          = File(path, FileProducerDefaults.defaultFsScripts)

        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member File(path, options) = File(path, FileProducerDefaults.defaultFsScripts @ options)
 

