namespace Camel.SubRoute

open Camel.Core
open Camel.Core.EngineParts

exception SubRouteException of string

type SubRouteOption =
     |  FailForMissingActiveSubRoute of bool

type SubRoute =
    class
        interface IProducer
        interface IConsumer
        interface IRegisterEngine
        internal new : string * SubRouteOption list -> SubRoute
    end

