namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileTransfer

/// Contains the FTP Consumer, reference "Camel.FTP.dll" to use it.
module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member Ftp : path : string * connection : string -> IConsumer
        
        /// Store a message's body in a File
        static member Ftp : path : string * connection : string * options : FtpOption list -> IConsumer

