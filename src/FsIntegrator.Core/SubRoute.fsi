namespace FsIntegrator

open FsIntegrator.Core
open FsIntegrator.Core.EngineParts
open FsIntegrator.Core.MessageOperations

exception SubRouteException of string

type SubRouteOption =
     |  FailForMissingActiveSubRoute of bool

type SubRoute =
    class
        interface IProducer
        interface IConsumer
        interface IRegisterEngine
        internal new : string * SubRouteOption list -> SubRoute
        internal new : StringMacro * SubRouteOption list -> SubRoute
    end

