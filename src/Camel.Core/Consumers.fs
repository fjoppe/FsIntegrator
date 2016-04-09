namespace Camel

open Camel.Core
open Camel.Core.General
open Camel.FileHandling

module FileConsumerDefaults =
    let afterSuccessDefault = fun _ -> FSScript.Empty

    let afterErrorDefault  = fun _ -> FSScript.Empty

    let defaultFsScripts = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault)]

module Consumers=
    type To = struct end
    type To with
        /// Store a message's body in a File
        static member File(path) =
            File(path, FileConsumerDefaults.defaultFsScripts) :> IConsumer
        
        /// Store a message's body in a File
        static member File(path, options) = 
            File(path, FileConsumerDefaults.defaultFsScripts @ options) :> IConsumer


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

