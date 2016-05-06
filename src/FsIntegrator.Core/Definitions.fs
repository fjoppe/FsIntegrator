namespace FsIntegrator.Core

open System
open FsIntegrator.Core
open FsIntegrator.Core.General
open FsIntegrator.Core.EngineParts
open FsIntegrator.Core.MessageOperations


type DefinitionType =
    | ProcessStep of (Message -> Message)
    | Consume     of IConsumer * (Message -> Message)
    | Choose      of ConditionalRoute list
and Route = {
        Id'       : Guid
        Producer' : IProducer
        Route'    : DefinitionType list
    }
    with
        static member Create p r = {Id' = Guid.NewGuid(); Producer' = p ; Route' = r}
        member          this.Id with get() = this.Id'
        member internal this.Producer with  get() = this.Producer'
        member internal this.SetProducer p = { this with Producer' = p}
        member internal this.Route with get() = this.Route'
        member internal this.SetRoute newRoute = {this with Route' = newRoute }

        member internal this.Register services =
            let reg data =
                match box(data) with
                | :? IRegisterEngine as re -> re.Register services
                | _ -> ()
            reg this.Producer'
            this.Route' |> List.iter(fun routeElm ->
                match routeElm with
                | Consume(consumerComponent, hook) -> reg consumerComponent
                | _ -> ())

and
    [<AbstractClass>]
    Intermediate() =
        abstract member DefinitionType : DefinitionType with get
and
    ConditionalRoute = {
        Id'        : Guid
        Route'     : DefinitionType list
        Condition' : BooleanMacro
    }
    with
        static member Create r c = {Id' = Guid.NewGuid(); Route' = r; Condition' = c}
        member          this.Id with get() = this.Id'
        member internal this.Route with get() = this.Route'
        member internal this.SetRoute newRoute = {this with Route' = newRoute } 

        member internal this.Register services =
            let reg data =
                match box(data) with
                | :? IRegisterEngine as re -> re.Register services
                | _ -> ()
            this.Route' |> List.iter(fun routeElm ->
                match routeElm with
                | Consume(consumerComponent, hook) -> reg consumerComponent
                | _ -> ())

        member internal this.Evaluate (message:Message) =
            this.Condition'.Evaluate message

