namespace FsIntegrator

open System.IO
open System.Timers
open FSharp.Data.UnitSystems.SI.UnitSymbols
open FsIntegrator.RouteEngine
open FsIntegrator.MessageOperations

exception FileComponentException of string

type FileMessageHeader = {
        FileInfo : FileInfo
    }


type FileOption =
    /// Delays operation after the route has been started
    |  InitialDelay of float<s>
    /// Specifies the minimum interval for which the file listener polls the specified endpoint. Default = 10 sec
    |   Interval of float<s>
    /// Specifies whether the listen-to folder needs to be created if it does not exist. Default = false
    |   CreatePathIfNotExists of bool
    /// When an incoming file is processed successfully in the route, these filesystem calls are executed. Can be used to move, rename or delete the incoming file. Default = move file to .success subdir.
    |   AfterSuccess of (Message -> FSScript)
    /// When an incoming file is processed with error(s) in the route, these filesystem calls are executed. Can be used to move, rename or delete the incoming file. Default = move file to .error subdir.
    |   AfterError of (Message -> FSScript)
    /// The amount of concurrent tasks which process in parallel
    |  ConcurrentTasks of int
    /// Which strategy to follow when the endpoint is failing
    |  EndpointFailureStrategy of EndpointFailureStrategy


type File =
    class
        interface IProducer
        interface IConsumer
        interface IRegisterEngine
        internal new : string * FileOption list -> File
        internal new : StringMacro * FileOption list -> File
    end

