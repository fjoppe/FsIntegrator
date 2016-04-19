namespace Camel.Core

open System
open Camel.Core.General
open Camel.Core.EngineParts
open Camel.Core.MessageOperations

type DefinitionType =
    | ProcessStep of (General.Message -> General.Message)
    | Consume     of IConsumer * (Message -> Message)
    | Choose      of ConditionalRoute list
and
    [<Sealed>]
    Route
and Route
    with
        static member internal Create : IProducer -> DefinitionType list -> Route
        member          Id          : Guid with  get
        member internal Producer    : IProducer with get
        member internal SetProducer : IProducer -> Route
        member internal Route       : DefinitionType list with get
        member internal SetRoute    : DefinitionType list -> Route
        member internal Register    : IEngineServices -> unit

and
    [<AbstractClassAttribute ()>]
    Intermediate =
    class
        new : unit -> Intermediate
        abstract member internal DefinitionType : DefinitionType with get
    end
and
    ConditionalRoute
and
    ConditionalRoute with
        static member   Create : DefinitionType list  -> BooleanMacro -> ConditionalRoute
        member          Id : Guid with get
        member internal Route : DefinitionType list with get
        member internal SetRoute : DefinitionType list -> ConditionalRoute
        member internal Register : IEngineServices -> unit
        member internal Evaluate : Message -> bool

