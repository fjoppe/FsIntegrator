//  ============================================================================================================
//
//  This script demonstrates some basic experiments with F# and type reflection.
//  For exceptions, we want to check whether the exception type should be handled or not.
//  The list of handled types may contain base-classes or interfaces of the actual exception instance.
//  Instead of being real fsharpish in this (state all types explicitly) 
//  we allow a base class to match with a subclass. That makes everything a bit more convenient.
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".." 
#r @"FsIntegrator.Core/bin/Debug/FsIntegrator.Core.dll"

open System

exception MyException1 of string
exception MyException2 of string

let myException = MyException1("test exception")

let targetTypes1 = [typeof<Exception>]
let targetTypes2 = [typeof<MyException1>]
let targetTypes3 = [typeof<MyException2>]


//  Some
targetTypes1 |> List.tryFind(fun t -> t.IsInstanceOfType(myException))

//  Some
targetTypes2 |> List.tryFind(fun t -> t.IsInstanceOfType(myException))

//  None
targetTypes3 |> List.tryFind(fun t -> t.IsInstanceOfType(myException))

