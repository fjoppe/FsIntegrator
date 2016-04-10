namespace Camel

open Camel.FileHandling
open Camel.SubRoute

module Producers =
    type From = struct end
    type From with
        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member File : path : string -> File

        /// Create a File-listener Producer, which listens to a folder on the local filesystem
        static member File : path : string * options : FileOption list -> File

        /// Create a Subroute, which is identified by the specified name
        static member SubRoute : name : string -> SubRoute

        /// Create a Subroute, which is identified by the specified name
        static member SubRoute : name : string * options : SubRouteOption list -> SubRoute


