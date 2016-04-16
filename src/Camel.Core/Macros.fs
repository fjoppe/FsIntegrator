namespace Camel

open System.IO
open System.Xml.XPath
open Camel.Core
open Camel.Core.General

exception MessageOperationException of string

#nowarn "0086" "0064"
module MessageOperations =

    type StringMacro = 
    /// Get value from message header
    |   Header of string
    /// Gets value from message body, for given xpath
    |   XPath  of string
    /// Gets a value, for the given custom function
    |   Func   of (Message -> string)
    /// Gets a literal value, constant string
    |   Literal  of string
    with
        member internal this.Substitute (message:Message) =
            match this with
            |   Header label ->
                if not(message.Headers.General.ContainsKey label) then ""
                else message.Headers.General.[label]
            |   XPath path   ->
                let xpath = XPathDocument(new StringReader(message.Body)).CreateNavigator()
                InternalUtility.substituteSingleXPath xpath path
            |   Func fnc    ->
                fnc(message)
            |   Literal  value -> value
        member this.ToInt with get() = ConvertToInt(this)
        member this.ToFloat with get() = ConvertToFloat(this)
    and IntegerMacro =
    /// Int constant
    |   Int of int
    /// Converts StringMacro output to int (string -> int), throws an exception if conversion fails
    |   ConvertToInt of StringMacro
    with
        member internal this.Substitute (message:Message) =
            match this with
            |   Int value           -> sprintf "%d" value
            |   ConvertToInt macro  -> macro.Substitute message
    and FloatMacro =
    /// Float constant
    |   Float of float
    /// Converts StringMacro output to float (string -> float)
    |   ConvertToFloat of StringMacro
        member internal this.Substitute (message:Message) =
            match this with
            |   Float value          -> sprintf "%f" value
            |   ConvertToFloat macro -> macro.Substitute message
    /// Compare two macros
    and Comparison<'T> =
    /// Compares a = b
    |   Equals of 'T * 'T
    /// Compares a < b
    |   LessThan of 'T * 'T
    /// Compares a > b
    |   GreaterThan of 'T * 'T
    and BooleanMacro<'T> =
    /// Bool constant
    |   Bool of bool
    /// Evaluates comparison: b = x operator y
    |   Evaluate of Comparison<'T>
    /// Inverse: b = !b
    |   Not of BooleanMacro<'T>
    /// Logical And: b = a && c
    |   And of BooleanMacro<'T> * BooleanMacro<'T>
    /// Logical Or: b = a || c
    |   Or of BooleanMacro<'T> * BooleanMacro<'T>

    type Operators = Operation with
        (* All allowed combination of Equals operator *)
        static member CompareEquals (Operation, l:StringMacro, r:string) = Evaluate(Equals(l,Literal(r)))
        static member CompareEquals (Operation, l:StringMacro, r:StringMacro) = Evaluate(Equals(l,r))
        static member CompareEquals (Operation, l:IntegerMacro, r:int) = Evaluate(Equals(l,Int(r)))
        static member CompareEquals (Operation, l:IntegerMacro, r:IntegerMacro) = Evaluate(Equals(l,r))
        static member CompareEquals (Operation, l:FloatMacro, r:float) = Evaluate(Equals(l,Float(r)))
        static member CompareEquals (Operation, l:FloatMacro, r:FloatMacro) = Evaluate(Equals(l,r))

        (* All allowed combination of Unequals operator *)
        static member CompareUnEquals (Operation, l:StringMacro, r:string) = Not(Operators.CompareEquals(Operation,l,r))
        static member CompareUnEquals (Operation, l:StringMacro, r:StringMacro) = Not(Operators.CompareEquals(Operation,l,r))
        static member CompareUnEquals (Operation, l:IntegerMacro, r:int) = Not(Operators.CompareEquals(Operation,l,r))
        static member CompareUnEquals (Operation, l:IntegerMacro, r:IntegerMacro) = Not(Operators.CompareEquals(Operation,l,r))
        static member CompareUnEquals (Operation, l:FloatMacro, r:float) = Not(Operators.CompareEquals(Operation,l,r))
        static member CompareUnEquals (Operation, l:FloatMacro, r:FloatMacro) = Not(Operators.CompareEquals(Operation,l,r))

        (* All allowed combination of LT operator *)
        static member CompareLT (Operation, l:StringMacro, r:string) = Evaluate(LessThan(l,Literal(r)))
        static member CompareLT (Operation, l:StringMacro, r:StringMacro) = Evaluate(LessThan(l,r))
        static member CompareLT (Operation, l:IntegerMacro, r:int) = Evaluate(LessThan(l,Int(r)))
        static member CompareLT (Operation, l:IntegerMacro, r:IntegerMacro) = Evaluate(LessThan(l,r))
        static member CompareLT (Operation, l:FloatMacro, r:float) = Evaluate(LessThan(l,Float(r)))
        static member CompareLT (Operation, l:FloatMacro, r:FloatMacro) = Evaluate(LessThan(l,r))

        (* All allowed combination of GT operator *)
        static member CompareGT (Operation, l:StringMacro, r:string) = Evaluate(GreaterThan(l,Literal(r)))
        static member CompareGT (Operation, l:StringMacro, r:StringMacro) = Evaluate(GreaterThan(l,r))
        static member CompareGT (Operation, l:IntegerMacro, r:int) = Evaluate(GreaterThan(l,Int(r)))
        static member CompareGT (Operation, l:IntegerMacro, r:IntegerMacro) = Evaluate(GreaterThan(l,r))
        static member CompareGT (Operation, l:FloatMacro, r:float) = Evaluate(GreaterThan(l,Float(r)))
        static member CompareGT (Operation, l:FloatMacro, r:FloatMacro) = Evaluate(GreaterThan(l,r))

    /// Equals comparison
    let inline (=) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareEquals : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Unequals comparison
    let inline (<>) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareUnEquals : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Less than comparison
    let inline (<) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareLT : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Greatre than comparison
    let inline (>) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareGT : ^T * ^N * ^M -> _) (Operation, l, r))

