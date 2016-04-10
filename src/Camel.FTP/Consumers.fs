namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileTransfer

module FtpConsumerDefaults =
    let afterSuccessDefault = fun _ -> FtpScript.Empty

    let afterErrorDefault  = fun _ -> FtpScript.Empty

    let defaultConsumerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault)]


module Consumers =
    type To = struct end
    type To with
        /// Store a message's body in a remote file
        static member Ftp(path, connection) =
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
        /// Store a message's body in a remote file
        static member Ftp(path, connection, options) = 
            Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

