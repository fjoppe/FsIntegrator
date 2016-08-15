namespace FsIntegrator

open System
open FsIntegrator.Routing.Types
open FsIntegrator.MessageOperations
open FSharp.Data.UnitSystems.SI.UnitSymbols

exception ActiveMQComponentException of string


type DestinationType =
    |   Queue
    |   Topic

type RedeliveryPolicy = {
        /// Maximum tries of redelivery
        MaxRedelivery   : int
        /// Initial delay (ms) for redelivery
        InitialDelay    : int
        /// Standard delay (ms) between redeliveries
        Delay           : int
    }

type AMQOption =
    /// Delays operation after the route has been started
    |  InitialDelay of float<s>
    /// The Uri to the ActiveMQ service, in the form of "activemq:tcp://yourhost:61616"
    |   Connection  of string
    /// The credentials for the ActiveMQ service
    |   Credentials of Credentials
    /// Target destination, queue or topic - refer to ActiveMQ documentation for details.
    |   DestinationType of DestinationType
    /// The amount of concurrent tasks which process in parallel
    |  ConcurrentTasks of int
    /// The redelivery policy
    |   RedeliveryPolicy of RedeliveryPolicy
    /// Which strategy to follow when the endpoint is failing
    |  EndpointFailureStrategy of EndpointFailureStrategy

type ActiveMQ = 
    class
        interface IProducer
        interface IConsumer
        interface IRegisterEngine
        internal new : string * AMQOption list -> ActiveMQ
        internal new : StringMacro * AMQOption list -> ActiveMQ
    end

