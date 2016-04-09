﻿namespace Camel.Core

exception ImplementationException of string
exception MissingMessageHook of string

open System
open Camel.Core
open Camel.Core.General

[<Interface>]
type IProducer = interface end

[<Interface>]
type IConsumer = interface end

type IEngineServices =
    abstract member producerList<'T when 'T :> IProducer> : unit -> 'T list

module EngineParts =

    [<Literal>]
    let WaitForHook = 10;

    type ProducerMessageHook = (Message -> unit)

    type ConsumerMessageHook = (Message -> unit)


    type ProducerState =
        | Stopped
        | Running


    type IProducerDriver =
        abstract member Start : unit -> IProducerDriver
        abstract member Stop  : unit -> IProducerDriver
        abstract member SetProducerHook : ProducerMessageHook -> IProducerDriver
        abstract member RunningState : ProducerState with get
        abstract member Register : IEngineServices -> IProducerDriver
        abstract member Validate : unit -> bool

    type IConsumerDriver = 
        abstract member GetConsumerHook : ConsumerMessageHook with get

    type ``Provide a Producer Driver`` =
        //  This should retrieve the component's implementation for producing messages
        abstract member ProducerDriver : IProducerDriver with get

    type ``Provide a Consumer Driver`` =
        //  This should retrieve the component's implementation for consuming messages
        abstract member ConsumerDriver : IConsumerDriver with get
        