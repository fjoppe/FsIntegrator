﻿namespace Camel.FileTransfer

open Camel.Core
open Camel.Core.General
open System
open FSharp.Data.UnitSystems.SI.UnitSymbols

exception FtpComponentException of string

type RemoteFile = {
        /// The filename
        Filename : string
        /// The folder on the FTP server where the file is located
        Folder   : string
        /// The fullpath - folder/file - on the FTP server
        FullPath : string
        /// The file size
        Size     : int64
        /// The file's creation date
        Created  : DateTime
        /// The file's modified date
        Modified : DateTime
    }


type FtpMessageHeader = {
        FileInfo : RemoteFile
    }


type FtpOption =
    /// Specifies the polling interval
    |   Interval of float<s>
    /// The FTP credentials
    |   Credentials of Credentials
    /// FtpScript which is run after succesfully processing a file
    |   AfterSuccess of (Message -> FtpScript)
    /// FtpScript which is run after a process error
    |   AfterError   of (Message -> FtpScript)
    /// The amount of concurrent tasks which process in parallel
    |  ConcurrentTasks of int

type Ftp = 
    class
        inherit ProducerConsumer
        internal new : string * string * FtpOption list -> Ftp
    end