namespace Camel.Core

open System
open General
open Camel.Core.EngineParts

        

type DefinitionType =
    | ProcessStep of (Message -> Message)
    | Consume     of (Message -> Message)
    with
        static member (=>=) (l:DefinitionType, r:DefinitionType)      = [l;r]
        static member (=>=) (l:DefinitionType, r:DefinitionType list) = l::r
        static member (=>=) (l:DefinitionType list, r:DefinitionType) = l @ [r]


type Route = {
        Id'       : Guid
        Producer' : IProducer
        Route'    : DefinitionType list
    }
    with
        static member Create p r = {Id' = Guid.NewGuid(); Producer' = p ; Route' = r}
        static member (=>=) (l:Route, r:DefinitionType) = { l with Route' = l.Route' @ [r]}
        static member (=>=) (l:Route, r:IConsumer) = { l with Route' = l.Route' @ [Consumer.CreateConsumerRoutePart(r)]}
        member          this.Id with get() = this.Id'
        member internal this.Producer with  get() = this.Producer'
        member internal this.SetProducer p = { this with Producer' = p}
        member internal this.Route with get() = this.Route'
and
    [<AbstractClass>]
    Producer() = 
        interface IProducer
        static member CreateProducerRoutePart (p:IProducer, r:DefinitionType) =
            match p :> obj with
            | :? ``Provide a Producer Driver`` -> Route.Create p [r]
            // A Producer must also implement RouteEngine.IProducerDriver, but make this implementation invisible via signature file.
            | _ -> raise(ImplementationException (sprintf "The Producer component '%s' does not comply with implementation rules." (p.GetType().Name)))

        static member (=>=) (p:IProducer, r:DefinitionType) = Producer.CreateProducerRoutePart(p,r)
and
    [<AbstractClass>]
    Consumer() =
        interface IConsumer
        static member CreateConsumerRoutePart (c:IConsumer) =
            match c :> obj with
            | :? ``Provide a Consumer Driver`` as cd -> 
                let consumberHook = cd.ConsumerDriver.GetConsumerHook
                let processConsumption message = 
                    message |> consumberHook
                    message
                Consume(processConsumption)
            // A Consumer must also implement RouteEngine.IConsumerDriver, but make this implementation invisible via signature file.
            | _ -> raise(ImplementationException (sprintf "The Consumer component '%s' does not comply with implementation rules." (c.GetType().Name)))

        static member (=>=) (r:DefinitionType, c:IConsumer) =
            r =>= Consumer.CreateConsumerRoutePart(c)

        static member (=>=) (r:DefinitionType list, c:IConsumer) =
            r =>= Consumer.CreateConsumerRoutePart(c)
and
    [<AbstractClass>]
    ProducerConsumer() =
        interface IProducer
        interface IConsumer
        static member (=>=) (p:IProducer, r:DefinitionType) =
            Producer.CreateProducerRoutePart(p,r)
        static member (=>=) (r:DefinitionType, c:IConsumer) =
            r =>= Consumer.CreateConsumerRoutePart(c)
        static member (=>=) (r:DefinitionType list, c:IConsumer) =
            r =>= Consumer.CreateConsumerRoutePart(c)
and
    [<AbstractClass>]
    Intermediate() =
        abstract member DefinitionType : DefinitionType with get
        static member (=>=) (l:Intermediate, r:DefinitionType)      = l.DefinitionType =>= r
        static member (=>=) (l:Intermediate, r:Intermediate)        = l.DefinitionType =>= r.DefinitionType
        static member (=>=) (l:Route, r:Intermediate)               = l =>= r.DefinitionType
        static member (=>=) (l:DefinitionType,  r:Intermediate)     = l =>= r.DefinitionType
        static member (=>=) (l:Producer,    r:Intermediate)         = l =>= r.DefinitionType
        static member (=>=) (l:ProducerConsumer, r:Intermediate)    = l =>= r.DefinitionType

