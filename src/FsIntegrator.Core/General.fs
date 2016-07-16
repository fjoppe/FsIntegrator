namespace FsIntegrator

open System
open System.Reflection

type Agent<'a>  = MailboxProcessor<'a>

exception MessageFormatException of string
    
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

[<Interface>]
type IProducer = interface end

[<Interface>]
type IConsumer = interface end

