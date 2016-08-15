namespace FsIntegrator

open FsIntegrator
open FsIntegrator.Core
open FsIntegrator.MessageOperations

/// Contains the FTP Consumer, reference "FsIntegrator.FTP.dll" to use it.
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
