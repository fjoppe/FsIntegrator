module FsIntegrator.Tests

open FsIntegrator.Core
open NUnit.Framework

[<Test>]
let ``hello returns 42`` () =
  let result = 42 // Library.hello 42
  printfn "%i" result
  Assert.AreEqual(42,result)

