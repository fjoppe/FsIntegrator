﻿namespace FsIntegrator

open System
open FsIntegrator
open FsIntegrator.Routing.Types

[<AutoOpen>]
module RouteEngine =
    type RouteInfo = {
        Id  : Guid
        RunningState : ProducerState
    }

    val RegisterRoute : Route -> unit
    val StartRoute    : Guid -> unit
    val StopRoute     : Guid -> unit
    val RouteInfo     : unit -> RouteInfo list

