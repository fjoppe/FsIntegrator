namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileHandling
open Camel.SubRoute

/// Contains the Core consumers, reference "Camel.Core.dll" to use it.
module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member File : string -> IConsumer
        
        /// Store a message's body in a File
        static member File : string * FileOption list -> IConsumer

        /// Send a message to an active subroute
        static member SubRoute : name: string -> IConsumer 

        /// Send a message to an active subroute
        static member SubRoute : name: string * options : SubRouteOption list -> IConsumer 

        /// Process a Message with a custom function
        static member Process : func : (Message -> unit) -> DefinitionType

        /// Process a Message with a custom function
        static member Process : func : (Message -> Message) -> DefinitionType

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> : mapper : Map<'a,string> * func : (Map<'a,string> -> Message -> Message) -> DefinitionType

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> : mapper : Map<'a,string> * func : (Map<'a,string> -> Message -> unit) -> DefinitionType



