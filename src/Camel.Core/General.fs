namespace Camel.Core

open System
open System.Reflection

type Agent<'a>  = MailboxProcessor<'a>

module General = 
    exception MessageFormatException of string
    
    type HeaderType = {
            General  : Map<string,string>
            Producer : Map<Guid,obj>
        }
        with
            static member Empty = { General = Map.empty; Producer = Map.empty}
            member this.SetProducer header = {this with Producer = Map.empty.Add(header.GetType().GUID, box(header))}
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
