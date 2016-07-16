namespace FsIntegrator

open FsIntegrator.Core


/// Contains the ActiveMQ consumer, reference "FsIntegrator.ActiveMQ.dll" to use it.
module Consumers=
    type To = struct end
    type To with
        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string -> IConsumer

        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ : destination : string * options : AMQOption list -> IConsumer

