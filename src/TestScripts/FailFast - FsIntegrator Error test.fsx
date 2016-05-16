//  ============================================================================================================
//
//  This script demonstrates two routes working together:
//  1.  Route from file to ActiveMQ
//  2.  Route from ActiveMQ to FTP enpoint with problems
//  3.  Problematic messages are send to ActiveMQ, to an error queue.
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
#r @"NLog/lib/net45/NLog.dll"

open System
open System.IO
open NLog
open FsIntegrator.Core
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FsIntegrator.Core.General
open FsIntegrator.Core.RouteEngine
open FsIntegrator.FileTransfer
open FsIntegrator.Queing
open FsIntegrator.Core.Definitions
open FsIntegrator.ErrorHandlers


//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
let nlogPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "./nlog.config"))
let logfile = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "logs", (sprintf "%s.log" __SOURCE_FILE__)))
let xmlConfig = new NLog.Config.XmlLoggingConfiguration(nlogPath)
xmlConfig.Variables.Item("logpath") <- Layouts.SimpleLayout(logfile)
LogManager.Configuration <- xmlConfig


//  Try this at home with your own configuration, for example: VirtualBox with Linux and ActiveMQ under ServiceMix
let amqConnection = "tcp://TestRemoteVM:61616"         // hostname of the ActiveMQ server
let amqCredentials = Credentials.Create "smx" "smx"    // credentials (ServiceMix)

//  We simulate FTP connection errors by entering the incorrect credentials
let ftpConnection = "TestRemoteVM/inbox"         // hostname and path on the ftp server
let ftpStorePath = "target.xml"                  // target filename and path
let ftpCredentials = Credentials.Create "test" "INVALID"  // credentials

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

//  The start of the two routes
let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../TestExamples/TestFullRoute") |> Path.GetFullPath


let DivertRoute = Error.Divert([typeof<Exception>]) =>= To.ActiveMQ("AlternateQueue",  [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])
let EquipRoute = Error.Equip([typeof<Exception>]) =>= To.ActiveMQ("ErrorQueue",  [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])

//  This sub route is re-used in both other routes
let Route1 = 
    From.SubRoute "subroute"
    =>= Process1
    =>= Process2


//  This route reads a file form the file system, and puts its content into a Queue on ActiveMQ
let Route2 =
    From.File(fileListenerPath)
    =>= ErrorHandlers([DivertRoute])
    =>= To.SubRoute("subroute")
    =>= To.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])


//  This route receives a message from ActiveMQ and stores it on an FTP folder
let Route3 = 
    From.ActiveMQ("testQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])
    =>= ErrorHandlers([DivertRoute])
    =>= To.SubRoute "subroute"
    =>= To.Ftp(ftpStorePath, ftpConnection, [FtpOption.Credentials(ftpCredentials)])

//  Acts on the alternate route
let Route4 =
    From.ActiveMQ("AlternateQueue", [AMQOption.Connection(amqConnection); AMQOption.Credentials(amqCredentials)])
    =>= ErrorHandlers([EquipRoute])
    =>= To.SubRoute "subroute"
    =>= To.Ftp(ftpStorePath, ftpConnection, [FtpOption.Credentials(ftpCredentials)])


let id1 = Route1.Id
let id2 = Route2.Id
let id3 = Route3.Id
let id4 = Route4.Id


RegisterRoute Route1
RegisterRoute Route2
RegisterRoute Route3
RegisterRoute Route4


RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id1
StartRoute id2
StartRoute id3
StartRoute id4
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)



