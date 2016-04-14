namespace Camel.Core

open System
open General


type DefinitionType =
    | ProcessStep of (General.Message -> General.Message)
    | Consume     of (Message -> Message)
    with
        static member ( =>= ) : l:DefinitionType * r:DefinitionType -> DefinitionType list
        static member ( =>= ) : l:DefinitionType * r:DefinitionType list -> DefinitionType list
        static member ( =>= ) : l:DefinitionType list * r:DefinitionType -> DefinitionType list

[<Sealed>]
type Route

type Route
    with
        static member ( =>= ) : l:Route * r:DefinitionType -> Route
        static member ( =>= ) : l:Route * r:IConsumer -> Route
        static member internal Create : IProducer -> DefinitionType list -> Route
        member          Id          : Guid with  get
        member internal Producer    : IProducer with get
        member internal SetProducer : IProducer -> Route
        member internal Route       : DefinitionType list with get
and
    [<AbstractClass>]
    Producer =
        interface IProducer
        new : unit -> Producer
        static member (=>=) : p:IProducer * r:DefinitionType -> Route
and
    [<AbstractClass>]
    Consumer =
        interface IConsumer 
        new : unit -> Consumer
        static member (=>=) : r:DefinitionType * c:IConsumer -> DefinitionType list
        static member (=>=) : r:DefinitionType list * c:IConsumer -> DefinitionType list
and
    [<AbstractClass>]
    ProducerConsumer =
        interface IProducer
        interface IConsumer
        new : unit -> ProducerConsumer
        static member (=>=) : p:IProducer * r:DefinitionType -> Route
        static member (=>=) : p:IProducer * c:IConsumer -> Route
        static member (=>=) : r:DefinitionType * c:IConsumer -> DefinitionType list
        static member (=>=) : r:DefinitionType list * c:IConsumer -> DefinitionType list
and
    [<AbstractClassAttribute ()>]
    Intermediate =
    class
        new : unit -> Intermediate
        abstract member internal DefinitionType : DefinitionType with get
        static member ( =>= ) : l:Intermediate * r:DefinitionType -> DefinitionType list
        static member ( =>= ) : l:Intermediate * r:Intermediate -> DefinitionType list
        static member ( =>= ) : l:Route * r:Intermediate -> Route
        static member ( =>= ) : l:DefinitionType * r:Intermediate -> DefinitionType list
        static member ( =>= ) : l:Producer * r:Intermediate -> Route
        static member ( =>= ) : l:ProducerConsumer * r:Intermediate -> Route
    end

