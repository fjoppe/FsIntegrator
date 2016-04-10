namespace Camel

open Camel.Core
open Camel.Queing

module ActiveMQConsumerDefaults =
    let defaultConsumerOptions = []


module Consumers=
    type To = struct end
    type To with
        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ(destination) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions) :> IConsumer

        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ(destination, options) = ActiveMQ(destination, ActiveMQConsumerDefaults.defaultConsumerOptions * options) :> IConsumer
