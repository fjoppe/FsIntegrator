namespace FsIntegrator

open FsIntegrator
open FsIntegrator.Core
open FsIntegrator.Core.General
open FsIntegrator.Core.MessageOperations

module FtpConsumerDefaults =
    let afterSuccessDefault = fun _ -> FtpScript.Empty

    let afterErrorDefault  = fun _ -> FtpScript.Empty

    let transferMode = FtpOption.TransferMode(TransferMode.Active)

    let defaultConsumerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault); transferMode]


module Consumers =
    type To = struct end
    type To with
        /// Store a message's body in a remote file
        static member Ftp(path : string, connection) =
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
        /// Store a message's body in a remote file
        static member Ftp(path : string, connection, options) = 
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

        /// Store a message's body in a remote file
        static member Ftp(path : StringMacro, connection) =
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
        /// Store a message's body in a remote file
        static member Ftp(path : StringMacro, connection, options) = 
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

