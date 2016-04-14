//  ============================================================================================================
//
//  This script demonstrates a file listener, which retreives an XML file and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  ============================================================================================================


#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.Core/bin/Debug/Camel.Core.dll"

open System.IO
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.RouteEngine

let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../TestExamples/TestFileListener")

let maps = Map.empty.Add("value", "//test/message") // xpath mapping in message

let Process1 = To.Process(fun m -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, (fun mp m -> printfn "processing: %s" mp.["value"]))


let route = (From.File fileListenerPath) =>= Process1 =>= Process2
let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

