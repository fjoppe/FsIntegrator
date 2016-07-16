//  ============================================================================================================
//
//  This script demonstrates two routes working together:
//  1.  Route from file to ActiveMQ
//  2.  Route from ActiveMQ to FTP 
//
//  And in between we print content to the screen.
//
//  Logs can be found under: ./src/TestScripts/logs/<scriptname>.log
//  
//  Prerequisites:
//      1.  A running installation of ActiveMQ (for example you can install apache ServiceMix)
//      2.  A running FTP service
//  
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"   // the order of #r to dll's is important, this one comes first
#r @"FsIntegrator.FTP/bin/Debug/FsIntegrator.FTP.dll"
#r @"FsIntegrator.ActiveMQ/bin/Debug/FsIntegrator.ActiveMQ.dll"

open System
open System.IO
open FsIntegrator
open FsIntegrator.Core
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FsIntegrator.RouteEngine
open FsIntegrator.MessageOperations


//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
#load "nlog.fsx"
NlogInit.With __SOURCE_DIRECTORY__ __SOURCE_FILE__


//  Try this at home with your own configuration, for example: VirtualBox with Linux and ActiveMQ under ServiceMix
let amqConnection = "tcp://TestRemoteVM:61616"              // hostname of the ActiveMQ server
let amqCredentials = Credentials.Create "admin" "admin"     // credentials (ServiceMix)

//  Try this at home with your own configuration, for example: VirtualBox with Linux and vsftpd
let ftpConnection = "TestRemoteVM/inbox"                     // hostname of the ftp server
let ftpStorePath = "target.xml"                  // target filename and path
let ftpCredentials = Credentials.Create "test" "test"  // credentials

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

//  The start of the two routes
let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../TestExamples/TestFullRoute") |> Path.GetFullPath


//  This sub route is re-used in both other routes
let Route1 = 
    From.SubRoute "subroute"
    =>= Process1
    =>= Process2


//  This route reads a file form the file system, and puts its content into a Queue on ActiveMQ
let Route2 =
    From.File(fileListenerPath)
    =>= To.SubRoute("subroute")
    =>= To.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])


//  This route receives a message from ActiveMQ and stores it on an FTP folder
let Route3 = 
    From.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])
    =>= To.SubRoute "subroute"
    =>= To.Ftp(ftpStorePath, ftpConnection, [FtpOption.Credentials(ftpCredentials); FtpOption.TransferMode(TransferMode.Active)])


let routes = [Route1; Route2; Route3]

routes |> List.iter(RegisterRoute)


RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
routes |> List.iter(fun r -> StartRoute r.Id)
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

//  At this point, any xml file in ./src/TestExamples/TestFullRoute/ will be processed (see prereqs)
//  If successful the file is copied to: ./src/TestExamples/TestFullRoute/.success
//  If error      the file is copied to: ./src/TestExamples/TestFullRoute/.error
//  Tip: you can move a file in the .success or .error folder back to the .../TestFullRoute/ folder

printfn "***************************"

StopRoute Route3.Id     //  closes activemq listener
StartRoute Route3.Id    //  reactivates activemq listener

printfn "***************************"

