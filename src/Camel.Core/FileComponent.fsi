namespace Camel.FileHandling

open Camel.Core
open Camel.Core.General
open System.IO
open System.Timers
open FSharp.Data.UnitSystems.SI.UnitSymbols

exception FileComponentException of string

type FileMessageHeader = {
        FileInfo : FileInfo
    }

type FileOption =
    /// Specifies the minimum interval for which the file listener polls the specified endpoint. Default = 10 sec
    |   Interval of float<s>
    /// Specifies whether the listen-to folder needs to be created if it does not exist. Default = false
    |   CreatePathIfNotExists of bool
    /// When an incoming file is processed successfully in the route, these filesystem calls are executed. Can be used to move, rename or delete the incoming file. Default = move file to .camel subdir.
    |   AfterSuccess of (Message -> FSScript)
    /// When an incoming file is processed with error(s) in the route, these filesystem calls are executed. Can be used to move, rename or delete the incoming file. Default = move file to .error subdir.
    |   AfterError of (Message -> FSScript)
    /// The amount of concurrent tasks which process in parallel
    |  ConcurrentTasks of int


type File =
    class
        inherit ProducerConsumer
        internal new : string * FileOption list -> File
    end

