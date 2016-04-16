//  ============================================================================================================
//
//  This script demonstrates the Camel.Net ActiveMQ listener, which recreives an XML message from ActiveMQ and 
//  sends its contents to the route. The Processors demonstate the receival of the message by printing it to stdout.
//
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  Logs can be found under: ./src/TestScripts/logs/<scriptname>.log
//
//  Prerequisites:
//      1.  A running installation of ActiveMQ (for example you can install apache ServiceMix)
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"Camel.Core/bin/Debug/Camel.Core.dll"   // the order of #r to dll's is important
#r @"Camel.ActiveMQ/bin/Debug/Camel.ActiveMQ.dll"
#r @"NLog/lib/net45/NLog.dll"

open System
open System.IO
open NLog
open Camel.Core
open Camel.Core.Definitions
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.Queing


//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
let nlogPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "./nlog.config"))
let logfile = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "logs", (sprintf "%s.log" __SOURCE_FILE__)))
let xmlConfig = new NLog.Config.XmlLoggingConfiguration(nlogPath)
xmlConfig.Variables.Item("logpath") <- Layouts.SimpleLayout(logfile)
LogManager.Configuration <- xmlConfig


//  Try this at home with your own configuration, for example: VirtualBox with Linux and ActiveMQ under ServiceMix
let connection = "tcp://TestRemoteVM:61616"         // hostname of the ActiveMQ server
let credentials = Credentials.Create "smx" "smx"    // credentials (ServiceMix)

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

let route =
    From.ActiveMQ("testQueue", [AMQOption.Connection(connection); AMQOption.Credentials(credentials)]) 
    =>= Process1 
    =>= Process2

let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

//  At this point, you may open the ActiveMQ webconsole, and send a message to queue "testQueue"
//  The message you can send is the content of "./src/TestExamples/TestFiles/test-message2.xml"
//  On my system, the activemq can be found under: http://TestRemoteVM:8181/activemqweb/ 
//  So:
//  1.  Open the webconsole
//  2.  Click on tab "queues"
//  3.  At the right of queue "testQueue" click on the "Send To" link
//  4.  Clear the text in box "Message Body" and paste the contents of your message
//  5.  Click "Send" button
//  The message should appear in the fsi output

StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

