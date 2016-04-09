//  ============================================================================================================
//
//  This script demonstrates some basic experiments with the fs monad.
//  The fs-monad provides a mini-language to do some simple filesystem operations.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.Core/bin/Camel.Core.dll"


open System
open Camel.Core
open Camel.Producers
open Camel.Consumers
open Camel.Core.General
open Camel.Core.RouteEngine
open Camel.FileHandling
open Camel.FileHandling.FileSystem


let fileCommands = fs {
        move "file" ".camel"
        rename "source" "target"
        delete "otherfile"
    }

fileCommands.GetType().GUID
typeof<FSScript>.GUID

let b = box(fileCommands)

let b2 = unbox<FSScript>(b)

let source = Map.empty.Add(typeof<FSScript>.GUID, box(fileCommands)).Add(typeof<int>.GUID, box(1))

let getMyValue<'a>() =
    let id = typeof<'a>.GUID
    if source.ContainsKey(id) then Some(unbox<'a>(source.[id]))
    else None

let r = getMyValue<FSScript>()
let r2 = getMyValue<float>()

