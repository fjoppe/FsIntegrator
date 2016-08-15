//  ============================================================================================================
//
//  This script demonstrates some basic experiments with the fs monad.
//  The fs-monad provides a mini-language to do some simple filesystem operations.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"


open System
open FsIntegrator
open FsIntegrator.Core
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FsIntegrator.RouteEngine


let fileCommands = fs {
        move "file" ".success"
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

