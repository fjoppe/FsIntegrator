namespace Camel.Core

open System
open NLog
open Camel.Core.EngineParts
open Camel.Core.General 

[<AutoOpen>]
module RouteEngine =
    let logger = LogManager.GetLogger("debug")

    let AsProducerDriver (ip:IProducer) =
        ip :?> ``Provide a Producer Driver``

    let rec ExecuteRouteForMessage (route : DefinitionType list) (initialMessage : Message) =
        route 
        |> List.fold(fun currentMessage definitionType ->
            match definitionType with
            |   ProcessStep func -> func currentMessage
            |   Consume(consumerComponent, func) -> func currentMessage
            |   Choose conditionList ->
                //  choose the first condition that complies
                let firstChoice = conditionList |> List.tryPick(fun condition -> if condition.Evaluate(currentMessage) then Some(condition) else None)
                match firstChoice with
                |   None           -> currentMessage   //  continue with input message
                |   Some condition -> ExecuteRouteForMessage (condition.Route) initialMessage               
        ) initialMessage 


    type RouteInfo = {
        Id  : Guid
        RunningState : ProducerState
    }
    with
        static member Create id state =
            { Id = id ; RunningState = state }

    type Command = 
        |   AddRoute   of Route
        |   StartRoute of Guid
        |   StopRoute  of Guid
        |   RouteInfoList  of AsyncReplyChannel<RouteInfo list>
        |   RouteList      of AsyncReplyChannel<Route list>

    
    type RouteItem = {
        Route          : Route
        ProducerDriver : IProducerDriver
    }
    with
        static member Create (route:Route) =
            let producerDriver = route.Producer |> AsProducerDriver
            producerDriver.ProducerDriver.SetProducerHook (ExecuteRouteForMessage route.Route)
            {Route = route; ProducerDriver = producerDriver.ProducerDriver}
        member this.ChangeRunningState producerDriver =
            {this with ProducerDriver = producerDriver}

    type State = {
        Routes : RouteItem list
    }
    with
        static member Create() =
            {Routes = []}

    type EngineServices(manager:Agent<Command>) =
        interface IEngineServices with
            member this.producerList<'T when 'T :> IProducer>() = 
                let fullList = manager.PostAndReply <| fun replyChannel -> RouteList(replyChannel)
                fullList 
                    |> List.filter(fun route -> route.Producer.GetType() = typeof<'T>)
                    |> List.map(fun route -> route.Producer :?> 'T)

    let RouteManager = 
        new Agent<Command>(fun inbox ->

            let runStateMap id (route:RouteItem) action =
                if route.Route.Id = id then route.ChangeRunningState <| action()
                else route

            let rec loop state = async {
                let exists(id:Guid) = state.Routes |> List.tryFind(fun e -> e.Route.Id = id) <> None
                let notExists(id) = not(exists(id))

                let! command = inbox.Receive()
                try
                    match command with
                    | AddRoute route ->
                        if notExists(route.Id) then
                            let newRouteItem = RouteItem.Create route
                            route.Register(EngineServices(inbox))
                            let newState = { state with Routes = state.Routes @ [newRouteItem]}
                            return! loop newState
                    | StartRoute id ->
                        if exists(id) then
                            state.Routes |> List.filter(fun r -> r.Route.Id  = id) |> List.iter(fun r -> r.ProducerDriver.Start())
                            return! loop state
                    | StopRoute id ->
                        if exists(id) then
                            state.Routes |> List.filter(fun r -> r.Route.Id  = id) |> List.iter(fun r -> r.ProducerDriver.Stop())
                            return! loop state
                    | RouteInfoList replyChannel ->
                        let overview = state.Routes |> List.map(fun routeItem -> RouteInfo.Create routeItem.Route.Id routeItem.ProducerDriver.RunningState)
                        replyChannel.Reply overview
                    | RouteList replyChannel ->
                        let overview = state.Routes |> List.map(fun routeItem -> routeItem.Route)
                        replyChannel.Reply overview
                with
                | _ as e -> printfn "Uncaught exception: %A" e
                return! loop state
            }
            loop (State.Create())
        )

    let RegisterRoute route = RouteManager.Post <| AddRoute route
    let StartRoute id = RouteManager.Post <| StartRoute id
    let StopRoute id = RouteManager.Post <| StopRoute id
    let RouteInfo() = RouteManager.PostAndReply <| fun replyChannel -> RouteInfoList(replyChannel)

    do RouteManager.Start()
