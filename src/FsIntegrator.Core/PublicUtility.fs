namespace FsIntegrator

open System
open FSharpx.Control

module Utility =
    type RestrictedResourcePool  = {
            Pool : BlockingQueueAgent<int> option
        }
        with
            static member Create size =
                if size = 1 then 
                    {Pool = None}
                else
                    let tokens = [1 .. size]
                    let agent = BlockingQueueAgent<int>(size)
                    tokens |> List.iter(fun item -> Async.RunSynchronously <| agent.AsyncAdd(item))
                    {Pool = Some(agent)}


            member this.PooledAction action =
                match this.Pool with
                |   None      ->  action()
                |   Some pool ->
                    //  this will block execution, until there is a token in the pool
                    let token = pool.AsyncGet() |> Async.RunSynchronously
                    //  process async, to enable the next iteration for List.iter
                    async {
                        try
                            action()
                        finally
                            //  always release the toke to the pool
                            pool.AsyncAdd(token) |> Async.RunSynchronously
                    } |> Async.RunSynchronously
                
    type ActionResponse =
        |   OK
        |   ERROR of Exception

    type FunctionResponse<'a> =
        |   Response of 'a
        |   ERROR of Exception
        with
            member this.GetResponseOrRaise() =
                match this with
                |   ERROR e -> raise e
                |   Response response -> response


    type ActionAsyncResponse = AsyncReplyChannel<ActionResponse>
    type FunctionsAsyncResponse<'a> = AsyncReplyChannel<FunctionResponse<'a>>

    