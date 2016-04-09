namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("Camel.ActiveMQ")>]
[<assembly: AssemblyProductAttribute("Camel.Net")>]
[<assembly: AssemblyDescriptionAttribute("F# DSL for Enterprise Integration Patterns")>]
[<assembly: AssemblyVersionAttribute("1.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0"
    let [<Literal>] InformationalVersion = "1.0"
