namespace Camel

open Camel.Core
open Camel.Queing

/// Contains the ActiveMQ consumer, reference "Camel.ActiveMQ.dll" to use it.
module Consumers=
    type To = struct end
    type To with
        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string -> IConsumer

        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string * options : AMQOption list -> IConsumer

