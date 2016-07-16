namespace FsIntegrator

open FsIntegrator
open FsIntegrator.RouteEngine
open FsIntegrator.MessageOperations

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

