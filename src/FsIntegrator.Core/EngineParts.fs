﻿namespace FsIntegrator.Routing

open System
open FsIntegrator

module Types =
    exception ImplementationException of string
    exception MissingMessageHook of string

    type IEngineServices =
        abstract member producerList<'T when 'T :> IProducer> : unit -> 'T list


    type ProducerMessageHook = (Message -> Message)

    type ConsumerMessageHook = (Message -> Message)


    type ProducerState =
        | Stopped
        | Running


    type IProducerDriver =
        abstract member Start : unit -> unit
        abstract member Stop  : unit -> unit
        abstract member SetProducerHook : ProducerMessageHook -> unit
        abstract member RunningState : ProducerState with get
    //        abstract member Register : IEngineServices -> unit
        abstract member Validate : unit -> bool

    type IConsumerDriver = 
        abstract member GetConsumerHook : ConsumerMessageHook with get

    type IRegisterEngine =
        abstract member Register : IEngineServices -> unit

    type ``Provide a Producer Driver`` =
        //  This should retrieve the component's implementation for producing messages
        abstract member ProducerDriver : IProducerDriver with get

    type ``Provide a Consumer Driver`` =
        //  This should retrieve the component's implementation for consuming messages
        abstract member ConsumerDriver : IConsumerDriver with get
        
    [<Literal>]
    let WaitForHook = 10
