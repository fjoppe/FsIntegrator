//  ============================================================================================================
//
//  This script demonstrates an FTP listener, which retreives an XML file and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.Core/bin/Debug/Camel.Core.dll"   // the order of #r to dll's is important
#r @"Camel.FTP/bin/Debug/Camel.FTP.dll"


open System
open System.IO
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.FileTransfer

//  Try this at home with your own configuration, for example: VirtualBox with Linux and vsftpd
let connection = "TestRemoteVM"                     // hostname of the ftp server
let fileListenerPath = "inbox"                      // (relative) path on the ftp server
let credentials = Credentials.Create "test" "test"  // credentials


let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

let route = (From.Ftp(fileListenerPath, connection, [FtpOption.Credentials(credentials)])) =>= Process1 =>= Process2
let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
