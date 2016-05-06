namespace FsIntegrator

open FsIntegrator.Core
open FsIntegrator.Core.General
open FsIntegrator.Core.MessageOperations
open FsIntegrator.FileTransfer

/// Contains the FTP Consumer, reference "FsIntegrator.FTP.dll" to use it.
module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member Ftp : path : string * connection : string -> IConsumer
        
        /// Store a message's body in a File
        static member Ftp : path : string * connection : string * options : FtpOption list -> IConsumer

        /// Store a message's body in a File
        static member Ftp : path : StringMacro * connection : string -> IConsumer
        
        /// Store a message's body in a File
        static member Ftp : path : StringMacro * connection : string * options : FtpOption list -> IConsumer
