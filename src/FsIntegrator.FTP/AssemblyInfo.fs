namespace System
open System.Reflection

[<assembly: AssemblyTitleAttribute("FsIntegrator.FTP")>]
[<assembly: AssemblyProductAttribute("FsIntegrator")>]
[<assembly: AssemblyDescriptionAttribute("F# DSL for Enterprise Integration Patterns")>]
[<assembly: AssemblyVersionAttribute("0.0.3")>]
[<assembly: AssemblyFileVersionAttribute("0.0.3")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "0.0.3"
    let [<Literal>] InformationalVersion = "0.0.3"
