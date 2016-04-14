namespace Camel

open System
open Camel.Queing

module ActiveMQProducerDefaults =
    let connection = Connection("activemq:tcp://localhost:61616")
    let destinationType = DestinationType(DestinationType.Queue)
    let concurrentTasks = ConcurrentTasks(1)

    let defaultProducerOptions = [connection;destinationType;concurrentTasks]

module Producers =
    type From = struct end
    type From with
        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProducerOptions)

        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination, options) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProducerOptions @ options)

