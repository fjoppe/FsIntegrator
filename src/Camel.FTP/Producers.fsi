namespace Camel

open Camel.FileTransfer

module Producers =
    type From = struct end
    type From with
        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member Ftp : path : string * connection : string -> Ftp

        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member Ftp : path : string * connection : string * options : FtpOption list -> Ftp




