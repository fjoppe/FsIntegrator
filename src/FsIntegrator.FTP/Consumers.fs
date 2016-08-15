namespace FsIntegrator

open FsIntegrator
//open FsIntegrator.MessageOperations
open FSharp.Data.UnitSystems.SI.UnitSymbols

    module FtpConsumerDefaults =
        let afterSuccessDefault = fun _ -> FtpScript.Empty

        let afterErrorDefault  = fun _ -> FtpScript.Empty

        let transferMode = FtpOption.TransferMode(TransferMode.Active)

        let endpointFailureStrategy = FtpOption.EndpointFailureStrategy(WaitAndRetryInfinite(5.0<s>))

        let defaultConsumerOptions = [AfterSuccess(afterSuccessDefault); AfterError(afterErrorDefault); transferMode; endpointFailureStrategy]


type To = struct end
type To with
    /// Store a message's body in a remote file
    static member Ftp(path : string, connection) =
        Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
    /// Store a message's body in a remote file
    static member Ftp(path : string, connection, options) = 
        Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

    /// Store a message's body in a remote file
    static member Ftp(path : StringMacro, connection) =
        Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions) :> IConsumer
        
    /// Store a message's body in a remote file
    static member Ftp(path : StringMacro, connection, options) = 
        Ftp(path, connection, FtpConsumerDefaults.defaultConsumerOptions @ options) :> IConsumer

