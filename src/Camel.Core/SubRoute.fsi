namespace Camel.SubRoute

open Camel.Core

exception SubRouteException of string

type SubRouteOption =
     |  FailForMissingActiveSubRoute of bool

type SubRoute =
    class
        inherit ProducerConsumer
        internal new : string * SubRouteOption list -> SubRoute
    end

