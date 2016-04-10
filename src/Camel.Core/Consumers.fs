namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileHandling
open Camel.SubRoute

module FileConsumerDefaults =
    let afterSuccessDefault = fun _ -> FSScript.Empty

    let afterErrorDefault  = fun _ -> FSScript.Empty

    let defaultConsumerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault)]

module SubRouteConsumerDefaults =
    let defaultConsumerOptions = []

module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member File(path) =
            File(path, FileConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
        /// Store a message's body in a File
        static member File(path, options) = 
            File(path, FileConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

        /// Send a message to an active subroute
        static member SubRoute(name) = 
            SubRoute(name, SubRouteConsumerDefaults.defaultConsumerOptions) :> IConsumer

        /// Send a message to an active subroute
        static member SubRoute(name, options) = 
            SubRoute(name, SubRouteConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

        /// Process a Message with a custom function
        static member Process (func : Message -> unit) = 
            ProcessStep(InternalUtility.CallAndReturnInput func)

        /// Process a Message with a custom function
        static member Process (func : Message -> Message) = 
            ProcessStep(func)

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> (mapper : Map<'a,string>, func : Map<'a,string> -> Message -> Message) =
            ProcessStep(InternalUtility.CallWithMapping mapper func)

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> (mapper : Map<'a,string>, func : Map<'a,string> -> Message -> unit) =
            ProcessStep(InternalUtility.CallAndReturnInput(InternalUtility.CallWithMapping mapper func))

