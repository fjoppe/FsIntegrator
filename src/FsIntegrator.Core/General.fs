namespace FsIntegrator

open System
open System.Reflection
open FSharp.Data.UnitSystems.SI.UnitSymbols

type Agent<'a>  = MailboxProcessor<'a>

exception MessageFormatException of string
exception ValidationException of string
    
type HeaderType = {
        General  : Map<string,string>
        Producer : Map<Guid,obj>
    }
    with
        static member Empty = { General = Map.empty; Producer = Map.empty}
        member this.SetProducer header = {this with Producer = Map.empty.Add(header.GetType().GUID, box(header))}
        member this.SetHeader (k, v) = { this with General = this.General.Add(k, v)}
        member this.GetProducer<'a>()  = 
            let id = typeof<'a>.GUID
            if this.Producer.ContainsKey(id) then Some(unbox<'a>(this.Producer.[id]))
            else None

type Message = {
        Headers : HeaderType
        Body    : string
    }
    with
        static member Empty = {Headers = HeaderType.Empty; Body = ""}
        member this.SetBody b = {this with Body = b}
        member this.SetHeader (k, v) =  { this with Headers = this.Headers.SetHeader(k,v)}
        member this.SetProducerHeader header = { this with Headers = this.Headers.SetProducer header}

type MessageMacroSubstition =
    |   MsgHeader of string
    |   MsgBody   of string

type Credentials = {
    Username : string
    Password : string
}
with
    static member Create username password = { Username = username; Password = password}


type EndpointFailureStrategy =
    |   WaitAndRetryInfinite of float<s>
    |   WaitAndRetryCountDownBeforeStop of float<s> * int
    |   StopImmediately
    with
        member this.Validate() =
            match this with
            |   WaitAndRetryInfinite wt -> if wt < 0.01<s> then raise (ValidationException "WaitAndRetryInfinite(wt): wt must be greater than 0.01<s>")
            |   WaitAndRetryCountDownBeforeStop(wt, cnt) -> 
                if wt < 0.01<s> then raise(ValidationException "WaitAndRetryCountDownBeforeStop (wt,cnt): wt must be greater than 0.01<s>")
                if cnt <1 then raise(ValidationException "WaitAndRetryCountDownBeforeStop (wt,cnt): cnt must be greater than 0")
            |   StopImmediately -> ()

[<Interface>]
type IProducer = interface end

[<Interface>]
type IConsumer = interface end

