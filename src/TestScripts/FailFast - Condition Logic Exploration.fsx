//  ============================================================================================================
//
//  This is to explore condition logic for the DSL.
//  These tests will be performed using a file listener (because its the easiest one)
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"Camel.Core/bin/Debug/Camel.Core.dll"   // the order of #r to dll's is important
#r @"NLog/lib/net45/NLog.dll"

open System
open System.IO
open NLog
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.FileHandling
open Camel.FileHandling.FileSystem
open Camel.Core.MessageOperations
open Camel.Core.Definitions
open Camel.Conditionals

//  Configure Nlog, logfile can be found under: ./src/TestScripts/logs/<scriptname>.log
let nlogPath = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "./nlog.config"))
let logfile = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "logs", (sprintf "%s.log" __SOURCE_FILE__)))
let xmlConfig = new NLog.Config.XmlLoggingConfiguration(nlogPath)
xmlConfig.Variables.Item("logpath") <- Layouts.SimpleLayout(logfile)
LogManager.Configuration <- xmlConfig

//  The start of the two routes
let fileListenerPath = Path.Combine( __SOURCE_DIRECTORY__, "../TestExamples/TestFullRoute") |> Path.GetFullPath

let maps = [("hi-message", "//root/message")] |> Map.ofList     // for xpath substitution
let Process1 = To.Process(fun (m:Message) -> printfn "P1: Message received: %A" m.Body)
let Process2 = To.Process(maps, fun mp m -> printfn "P2: Processing: %s" mp.["hi-message"])

//  Some condition tests
let c1 = Header("property") &= Header("property")
let c2 = Header("property") &= "test"
let c3 = Header("property").ToInt &= Int(11)
let c4 = Header("property").ToInt &= (12)
let c5 = Header("property").ToFloat &= Float(13.0)
let c6 = Header("property").ToInt &= 14
let c7 = Header("prop1").ToInt <&> XPath("//root/message").ToInt

To.Choose [
    When(Header("property") &= "test1") 
        =>= Process1 
        =>= Process2
    When(Header("property").ToInt &= 14) 
        =>= Process1 
        =>= Process2
]


//let Route1 = 
//    From.File(fileListenerPath, [FileOption.AfterSuccess(NoFileScript); FileOption.AfterError(NoFileScript)])   // don't relocate the file
//        =>= Choose [
//            When(Header("label") = "test" )
//        ]
//        
//        Process1 
//        =>= Process2



