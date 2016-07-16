namespace FsIntegrator

open System
open FSharp.Data.UnitSystems.SI.UnitSymbols

module ActiveMQProducerDefaults =
    let connection = Connection("activemq:tcp://localhost:61616")
    let destinationType = DestinationType(DestinationType.Queue)
    let concurrentTasks = ConcurrentTasks(1)
    let redeliveryPolicy = RedeliveryPolicy({MaxRedelivery = 0; InitialDelay = 0; Delay = 0})

    let endpointFailureStrategy = AMQOption.EndpointFailureStrategy(WaitAndRetryInfinite(5.0<s>))

    let defaultProducerOptions = [connection;destinationType;concurrentTasks; redeliveryPolicy]

module Producers =
    type From = struct end
    type From with
        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination : string) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProducerOptions)

        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination : string, options) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProducerOptions @ options)

