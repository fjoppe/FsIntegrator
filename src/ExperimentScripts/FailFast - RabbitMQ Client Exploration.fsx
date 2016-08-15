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

#r @"RabbitMQ.Client.3.6.3/lib/net45/RabbitMQ.Client.dll"

open System
open RabbitMQ.Client

let factory = new ConnectionFactory()
factory.UserName <- "guest"
factory.Password <- "guest"
factory.VirtualHost <- "/"
factory.HostName <- "testremotevm";

let conn = factory.CreateConnection()
let model = conn.CreateModel()

model.ExchangeDeclare("testExchange", ExchangeType.Direct)
model.QueueDeclare("testQueue", false, false, false, null)
model.QueueBind("testQueue", "testExchange", "myroutingkey")

let messageBodyBytes = System.Text.Encoding.UTF8.GetBytes("Hello, world!")
model.BasicPublish("testExchange", "myroutingkey", null, messageBodyBytes)


