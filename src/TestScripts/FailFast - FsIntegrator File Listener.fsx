//  ============================================================================================================
//
//  This script demonstrates a file listener, which retreives an XML file and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  ============================================================================================================


#I __SOURCE_DIRECTORY__
#I ".." 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"

//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
#load "nlog.fsx"
NlogInit.With __SOURCE_DIRECTORY__ __SOURCE_FILE__

open System.IO
open FsIntegrator.Core
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FsIntegrator.RouteEngine


let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../TestExamples/TestFileListener")

let maps = Map.empty.Add("value", "//test/message") // xpath mapping in message

let Process1 = To.Process(fun m -> printfn "message received: %A" m.Body)
let Process2 = To.Process(maps, (fun mp m -> printfn "processing: %s" mp.["value"]))


let route = From.File fileListenerPath =>= Process1 =>= Process2
let id = route.Id

RegisterRoute route

RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StartRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)
StopRoute id
RouteInfo() |> List.iter(fun e -> printfn "%A\t%A" e.Id e.RunningState)

