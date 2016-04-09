//  ============================================================================================================
//
//  This script demonstrates an FTP listener, which retreives an XML file and sends its contents to
//  the route. The Processors demonstate the receival of the message.
//  The RouteEngine demonstrates simple start, stop and info commands.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"Camel.ActiveMQ/bin/Debug/Apache.NMS.dll"
#r @"Camel.ActiveMQ/bin/Debug/Apache.NMS.ActiveMQ.dll"
#r @"Camel.Core/bin/Debug/Camel.Core.dll"
#r @"Camel.ActiveMQ/bin/Debug/Camel.ActiveMQ.dll"


open System
open Apache.NMS
open Apache.NMS.Util
open Apache.NMS.ActiveMQ.Commands

let connecturi = new Uri("activemq:tcp://TestRemoteVM:61616")

let factory = NMSConnectionFactory(connecturi)

let connection = factory.CreateConnection("smx","smx")
let session = connection.CreateSession()
let destination = SessionUtil.GetDestination(session, "testQueue", DestinationType.Queue)


let consumer = session.CreateConsumer(destination)

let m = consumer.Receive()

consumer.add_Listener(fun m -> 
    match m with
    |   :? ActiveMQTextMessage as s -> printfn "received message: %s" <| s.Text
    |   _   -> printfn "unexpected message: %s" <| m.ToString()
    )


let producer = session.CreateProducer(destination)
connection.Start()

let message = session.CreateTextMessage("hello world")

message.GetType().FullName

producer.Send(message)

