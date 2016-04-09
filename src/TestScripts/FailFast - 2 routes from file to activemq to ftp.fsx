//  ============================================================================================================
//
//  This script demonstrates two routes working together:
//  1.  Route from file to ActiveMQ
//  2.  Route from ActiveMQ to FTP 
//
//  And in between we print content to the screen.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.Core/bin/Debug/Camel.Core.dll"   // the order of #r to dll's is important
#r @"Camel.FTP/bin/Debug/Camel.FTP.dll"
#r @"Camel.ActiveMQ/bin/Debug/Camel.ActiveMQ.dll"

open System
open System.IO
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.FileTransfer
open Camel.Queing

//  Try this at home with your own configuration, for example: VirtualBox with Linux and ActiveMQ under ServiceMix
let amqConnection = "tcp://TestRemoteVM:61616"         // hostname of the ActiveMQ server
let amqCredentials = Credentials.Create "smx" "smx"    // credentials (ServiceMix)

//  Try this at home with your own configuration, for example: VirtualBox with Linux and vsftpd
let ftpConnection = "TestRemoteVM"                     // hostname of the ftp server
let ftpStorePath = "inbox/target.xml"                  // target filename and path
let ftpCredentials = Credentials.Create "test" "test"  // credentials

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

//  The start of the two routes
let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../Testing/TestFullRoute") |> Path.GetFullPath



//  First route reads a file form the file system, and puts its content into a Queue on ActiveMQ
let Route1 =
    From.File fileListenerPath
    =>= Process1 
    =>= Process2
    =>= To.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])


//  Second route receives a message from ActiveMQ and stores it on an FTP folder
let Route2 = 
    From.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])
    =>= Process1 
    =>= Process2
    =>= To.Ftp(ftpStorePath, ftpConnection, [FtpOption.Credentials(ftpCredentials)])


let id1 = Route1.Id
let id2 = Route2.Id

RegisterRoute Route1
RegisterRoute Route2

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id1
StartRoute id2

printfn "***************************"

StopRoute id2   //  closes activemq listener
StartRoute id2  //  reactivates activemq listener

printfn "***************************"

