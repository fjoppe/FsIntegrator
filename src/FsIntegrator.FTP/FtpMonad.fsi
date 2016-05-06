namespace FsIntegrator.FileTransfer

open System.Net.FtpClient

[<Sealed>]
type FtpScript
type FtpScript
    with
        static member internal Empty : FtpScript
        static member internal Run : client : FtpClient -> ftp : FtpScript -> unit

type FtpBuilder =
    class
        member Yield : 'a -> FtpScript

        /// Move file to target
        [<CustomOperation("move", MaintainsVariableSpace = true)>]
        member Move : fs : FtpScript * source : string * target : string -> FtpScript

        /// Rename file to target filename
        [<CustomOperation("rename", MaintainsVariableSpace = true)>]
        member Rename : fs : FtpScript * source : string * target : string -> FtpScript

        /// Delete current file
        [<CustomOperation("delete", MaintainsVariableSpace = true)>]
        member Delete : fs : FtpScript * target : string -> FtpScript

        /// Create a directory
        [<CustomOperation("createdir", MaintainsVariableSpace = true)>]
        member MakeDir : fs : FtpScript * target : string -> FtpScript

        /// Delete a directory
        [<CustomOperation("deletedir", MaintainsVariableSpace = true)>]
        member RemoveDir : fs : FtpScript * target : string -> FtpScript

        /// Move a directory
        [<CustomOperation("movedir", MaintainsVariableSpace = true)>]
        member MoveDir : fs : FtpScript * source : string * target : string -> FtpScript

        /// Transfer a local file to the remote system
        [<CustomOperation("putfile", MaintainsVariableSpace = true)>]
        member PutFile : fs : FtpScript * source : string * target : string -> FtpScript

        /// Transfer a remote file to the local system
        [<CustomOperation("getfile", MaintainsVariableSpace = true)>]
        member GetFile : fs : FtpScript * source : string * target : string -> FtpScript
    end


module RemoteFileSystem =  
    val ftp : FtpBuilder

    /// Empty FTP Script (do nothing)
    val NoFTPScript<'a> : 'a -> FtpScript
