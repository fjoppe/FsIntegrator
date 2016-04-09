namespace Camel

open System
open Camel.Queing

module ActiveMQProducerDefaults =
    let connection = Connection("activemq:tcp://localhost:61616")
    let destinationType = DestinationType(DestinationType.Queue)

    let defaultProperties = [connection;destinationType]

module Producers =
    type From = struct end
    type From with
        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProperties)

        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ(destination, options) = ActiveMQ(destination, ActiveMQProducerDefaults.defaultProperties @ options)

