namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Camel.ActiveMQ")>]
[<assembly: AssemblyProductAttribute("Camel.Net")>]
[<assembly: AssemblyDescriptionAttribute("F# DSL for Enterprise Integration Patterns")>]
[<assembly: AssemblyVersionAttribute("0.0.2")>]
[<assembly: AssemblyFileVersionAttribute("0.0.2")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.2"
    let [<Literal>] InformationalVersion = "0.0.2"
