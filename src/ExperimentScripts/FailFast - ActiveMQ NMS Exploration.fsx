//  ============================================================================================================
//
//  This script explores the Apache NMS ActiveMQ library, how to use this from F#
//
//  Prerequisites:
//      1.  A running installation of ActiveMQ (for example you can install apache ServiceMix)
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#I "../../packages" 
#r @"FsIntegrator.ActiveMQ/bin/Debug/Apache.NMS.dll"
#r @"FsIntegrator.ActiveMQ/bin/Debug/Apache.NMS.ActiveMQ.dll"
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"
#r @"FsIntegrator.ActiveMQ/bin/Debug/FsIntegrator.ActiveMQ.dll"

open System
open Apache.NMS
open Apache.NMS.Util
open Apache.NMS.ActiveMQ.Commands


//  ActiveMQ endpoint and credentials
let connecturi = new Uri("activemq:tcp://TestRemoteVM:61616")
let factory = NMSConnectionFactory(connecturi)
let connection = factory.CreateConnection("smx","smx")
let session = connection.CreateSession()
let destination = SessionUtil.GetDestination(session, "testQueue", DestinationType.Queue)


//  Consumer with message listener
let consumer = session.CreateConsumer(destination)
consumer.add_Listener(fun m -> 
    match m with
    |   :? ActiveMQTextMessage as s -> printfn "received message: %s" <| s.Text
    |   _   -> printfn "unexpected message: %s" <| m.ToString()
    )

connection.Start()

let message = session.CreateTextMessage("hello world")

//  Producer, send a message
let producer = session.CreateProducer(destination)
producer.Send(message)

