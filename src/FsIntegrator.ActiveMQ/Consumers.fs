namespace FsIntegrator

open FsIntegrator.MessageOperations
open FSharp.Data.UnitSystems.SI.UnitSymbols


    module ActiveMQConsumerDefaults =
        let endpointFailureStrategy = AMQOption.EndpointFailureStrategy(WaitAndRetryInfinite(5.0<s>))

        let defaultConsumerOptions = [endpointFailureStrategy]

type To = struct end
type To with
    /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
    static member ActiveMQ(destination : string) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions) :> IConsumer

    /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
    static member ActiveMQ(destination : string, options) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

    /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
    static member ActiveMQ(destination : StringMacro) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions) :> IConsumer

    /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
    static member ActiveMQ(destination : StringMacro, options) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer
