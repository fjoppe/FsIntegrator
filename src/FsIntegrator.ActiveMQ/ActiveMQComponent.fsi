namespace FsIntegrator.Queing

open System
open FsIntegrator.Core
open FsIntegrator.Core.EngineParts
open FsIntegrator.Core.General
open FsIntegrator.Core.MessageOperations

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


type ActiveMQ = 
    class
        interface IProducer
        interface IConsumer
        interface IRegisterEngine
        internal new : string * AMQOption list -> ActiveMQ
        internal new : StringMacro * AMQOption list -> ActiveMQ
    end

