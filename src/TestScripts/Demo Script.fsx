//  ============================================================================================================
//
//  This script demonstrates the DSL.
//
//  ============================================================================================================


#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"

open FsIntegrator.FileHandling
open FsIntegrator.FileHandling.FileSystem
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FSharp.Data.UnitSystems.SI.UnitSymbols
open FsIntegrator.Core.Definitions

let fileCommands = fs {
        move "file" ".success"
        rename "source" "target"
        delete "otherfile"
    }

From.File(@"C:\tmp\myfolder", [FileOption.AfterSuccess(fun m -> fileCommands)]) 
    =>= To.Process(fun m -> printf "Hello Whirled")


