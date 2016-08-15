//  ============================================================================================================
//
//  This script demonstrates that "Fsharpx.Core.BlockingQueue" may be used as a resource pool
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../.." 
#r @"packages/FSharpx.Async.1.12.0/lib/net40/FSharpx.Async.dll"

open FSharpx.Control

let size = 4
let tokens = [1 .. size]
let agent = BlockingQueueAgent<int>(size)
tokens |> List.iter(
    fun item -> 
        printfn "adding: token: %d" item
        Async.RunSynchronously <| agent.AsyncAdd(item)
        printfn "added: token: %d" item
    )

printfn "ready"
