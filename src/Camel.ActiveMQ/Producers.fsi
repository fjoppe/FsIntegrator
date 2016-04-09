namespace Camel

open Camel.Queing

module Producers =
    type From = struct end
    type From with
        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string -> ActiveMQ

        /// Create an ActiveMQ listener, which listens to message on the specified destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string * options : AMQOption list -> ActiveMQ


