//  ============================================================================================================
//
//  This script demonstrates the DSL.
//
//  ============================================================================================================


#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"

open FsIntegrator
open FsIntegrator.Core
open FsIntegrator.Producers
open FsIntegrator.Consumers
open FSharp.Data.UnitSystems.SI.UnitSymbols


let fileCommands = fs {
        move "file" ".success"
        rename "source" "target"
        delete "otherfile"
    }

From.File(@"C:\tmp\myfolder", [FileOption.AfterSuccess(fun m -> fileCommands)]) 
    =>= To.Process(fun m -> printf "Hello Whirled")


