//  ============================================================================================================
//
//  This script demonstrates an FTP listener, which retreives an XML file and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  Logs can be found under: ./src/TestScripts/logs/<scriptname>.log
//
//  Prerequisites:
//      1.  A running FTP Server
//      2.  A user account test/test
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"   // the order of #r to dll's is important
#r @"FsIntegrator.FTP/bin/Debug/FsIntegrator.FTP.dll"
//#r @"NLog/lib/net45/NLog.dll"

open System
open System.IO
//open NLog
open FsIntegrator.Core
open FsIntegrator.Core.Definitions
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FsIntegrator.Core.General
open FsIntegrator.Core.RouteEngine
open FsIntegrator.FileTransfer

//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
#load "nlog.fsx"
NlogInit.With __SOURCE_DIRECTORY__ __SOURCE_FILE__


//  Try this at home with your own configuration, for example: VirtualBox with Linux and vsftpd
let connection = "TestRemoteVM"                     // hostname of the ftp server
let fileListenerPath = "inbox"                      // (relative) path on the ftp server
let credentials = Credentials.Create "test" "test"  // credentials


let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

let route = 
    From.Ftp(fileListenerPath, connection, [FtpOption.Credentials(credentials); FtpOption.TransferMode(TransferMode.Passive)])
    =>= Process1
    =>= Process2

let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id

//  At this point, you can put a file in /home/test/inbox on the FTP system, which will be picked up by the route.
//  As an example file, you can use: "./src/TestExamples/TestFiles/test-message2.xml"
//  When the file is processed, it will be moved to /home/test/inbox/.success or /home/test/inbox/.error

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
