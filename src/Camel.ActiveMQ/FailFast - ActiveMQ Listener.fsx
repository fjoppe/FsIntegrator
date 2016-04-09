//  ============================================================================================================
//
//  This script demonstrates an ActiveMQ listener, which retreives an XML message and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.Core/bin/Camel.Core.dll"   // the order of #r to dll's is important
#r @"Camel.ActiveMQ/bin/Camel.ActiveMQ.dll"


open System
open System.IO
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.Queing

//  Try this at home with your own configuration, for example: VirtualBox with Linux and ActiveMQ under ServiceMix
let connection = "tcp://TestRemoteVM:61616"         // hostname of the ActiveMQ server
let credentials = Credentials.Create "smx" "smx"    // credentials (ServiceMix)

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution

let Process1 = To.Process(fun (m:Message) -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "processing: %s" mp.["hi-message"])

let route = (From.ActiveMQ("testQueue", [AMQOption.Connection(connection); AMQOption.Credentials(credentials)])) =>= Process1 =>= Process2
let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

