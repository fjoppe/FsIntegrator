namespace Camel.FileTransfer

open System.IO
open System.Net.FtpClient
open FSharp.Core
open Camel.Core.General

type FtpCommand =
    |   Move      of string * string
    |   Rename    of string * string
    |   Delete    of string
    |   CreateDir of string
    |   DeleteDir of string
    |   MoveDir   of string * string
    |   PutFile   of string * string
    |   GetFile   of string * string


type FtpScript = {
        CommandList : FtpCommand list
    }
    with
        member this.Add item = { this with CommandList = this.CommandList @ [item]}
        static member internal Empty = { CommandList = []}
        static member internal Run (client:FtpClient) (ftp:FtpScript) =
            let processCommand = 
                function
                |   Move(source, target)    -> client.Rename(source, target)
                |   Rename(source, target)  -> client.Rename(source, target)
                |   Delete(target)          -> client.DeleteFile(target)
                |   CreateDir(target)       -> client.CreateDirectory(target)
                |   DeleteDir(target)       -> client.DeleteDirectory(target)
                |   MoveDir(source, target) -> client.Rename(source, target)
                |   PutFile(source, target) ->
                    use sourceStream = File.OpenRead(source)
                    use targetStream = client.OpenWrite(target)
                    sourceStream.CopyTo(targetStream)
                    targetStream.Close()
                    sourceStream.Close()
                |   GetFile(source, target) ->
                    use sourceStream = client.OpenWrite(target)
                    use targetStream = File.OpenRead(source)
                    sourceStream.CopyTo(targetStream)
                    targetStream.Close()
                    sourceStream.Close()
            ftp.CommandList
            |> List.iter(processCommand)

type FtpBuilder() = 
    member this.Yield item = FtpScript.Empty

    member this.Move((fs:FtpScript), (source:string), (target:string)) =
        fs.Add(Move(source, target))

    member this.Rename((fs:FtpScript), (source:string), (target:string)) =
        fs.Add(Rename(source, target))

    member this.Delete((fs:FtpScript), (target:string)) =
        fs.Add(Delete(target))

    member this.MakeDir((fs:FtpScript), target) =
        fs.Add(CreateDir(target))

    member this.RemoveDir((fs:FtpScript), target) =
        fs.Add(DeleteDir(target))

    member this.MoveDir((fs:FtpScript), source, target) =
        fs.Add(MoveDir(source, target))

    member this.PutFile((fs:FtpScript), source, target) =
        fs.Add(PutFile(source, target))

    member this.GetFile((fs:FtpScript), source, target) =
        fs.Add(GetFile(source, target))

module RemoteFileSystem =  
    let ftp = FtpBuilder()

