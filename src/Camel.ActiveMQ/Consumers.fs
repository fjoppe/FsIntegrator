﻿namespace Camel

open Camel.Core
open Camel.Queing

module Consumers=
    type To = struct end
    type To with
        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ(destination) = ActiveMQ(destination, []) :> IConsumer

        /// Send Message to an ActiveMQ destination. A destination is a topic or queue.
        static member ActiveMQ(destination, options) = ActiveMQ(destination, options) :> IConsumer
