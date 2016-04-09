namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileHandling

module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member File : string -> IConsumer
        
        /// Store a message's body in a File
        static member File : string * FileOption list -> IConsumer

        /// Process a Message with a custom function
        static member Process : func : (Message -> unit) -> DefinitionType

        /// Process a Message with a custom function
        static member Process : func : (Message -> Message) -> DefinitionType

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> : mapper : Map<'a,string> * func : (Map<'a,string> -> Message -> Message) -> DefinitionType

        /// Process a Message with a custom function, using an XPath mapping
        static member Process<'a when 'a : comparison> : mapper : Map<'a,string> * func : (Map<'a,string> -> Message -> unit) -> DefinitionType



