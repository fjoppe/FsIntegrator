namespace Camel.Core

open System
open Camel.Core
open Camel.Core.General
open Camel.Core.EngineParts


type Operators = Operation with
    static member internal CreateProducerRoutePart (p:IProducer, r:DefinitionType) =
        match p :> obj with
        | :? ``Provide a Producer Driver`` -> Route.Create p [r]
        // A Producer must also implement RouteEngine.IProducerDriver, but make this implementation invisible via signature file.
        | _ -> raise(ImplementationException (sprintf "The Producer component '%s' does not comply with implementation rules." (p.GetType().Name)))
    
    static member internal CreateConsumerRoutePart (c:IConsumer) =
        match c :> obj with
        | :? ``Provide a Consumer Driver`` as cd -> 
            let consumberHook = cd.ConsumerDriver.GetConsumerHook
            let processConsumption message = 
                message |> consumberHook
            Consume(c, processConsumption)
        // A Consumer must also implement RouteEngine.IConsumerDriver, but make this implementation invisible via signature file.
        | _ -> raise(ImplementationException (sprintf "The Consumer component '%s' does not comply with implementation rules." (c.GetType().Name)))


    (*  Definition Type operations *)
    static member FlowOperator (Operation, l:DefinitionType, r:DefinitionType) = [l;r]
    static member FlowOperator (Operation, l:DefinitionType, r:DefinitionType list) = l::r
    static member FlowOperator (Operation, l:DefinitionType list, r:DefinitionType) = l @ [r]
    
    (*  Route operations *)
    static member FlowOperator (Operation, l:Route, r:DefinitionType) = l.SetRoute <| l.Route @ [r]
    static member FlowOperator (Operation, l:Route, r:IConsumer) = l.SetRoute <| l.Route @ [Operators.CreateConsumerRoutePart(r)]

    (*  Producer operations *)
    static member FlowOperator (Operation, p:IProducer, r:DefinitionType) = Operators.CreateProducerRoutePart(p,r)
    static member FlowOperator (Operation, p:IProducer, c:IConsumer) = Operators.CreateProducerRoutePart(p, Operators.CreateConsumerRoutePart(c))

    (*  Consumer operations *)
    static member FlowOperator (Operation, r:DefinitionType, c:IConsumer) =      Operators.FlowOperator(Operation, r,(Operators.CreateConsumerRoutePart(c)))
    static member FlowOperator (Operation, r:DefinitionType list, c:IConsumer) = Operators.FlowOperator(Operation, r, Operators.CreateConsumerRoutePart(c))

    (*  Intermediate operations *)
    static member FlowOperator (Operation, l:Intermediate, r:DefinitionType)      = Operators.FlowOperator(Operation, l.DefinitionType, r)
    static member FlowOperator (Operation, l:Intermediate, r:Intermediate)        = Operators.FlowOperator(Operation, l.DefinitionType, r.DefinitionType)
    static member FlowOperator (Operation, l:Route, r:Intermediate)               = Operators.FlowOperator(Operation, l, r.DefinitionType)
    static member FlowOperator (Operation, l:DefinitionType,  r:Intermediate)     = Operators.FlowOperator(Operation, l, r.DefinitionType)
    static member FlowOperator (Operation, l:IProducer,    r:Intermediate)         = Operators.FlowOperator(Operation, l, r.DefinitionType)

#nowarn "0064"
module Definitions =
    let inline (=>=) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member FlowOperator : ^T * ^N * ^M -> _) (Operation, l, r))

