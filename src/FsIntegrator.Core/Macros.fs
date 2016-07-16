namespace FsIntegrator

open System.IO
open System.Xml.XPath


exception MessageOperationException of string

#nowarn "0086" "0064"
module MessageOperations =

    /// Interface for any primitive type which can be substituted with a Message
    type ISubstitutable = 
        interface
            abstract member Substitute : Message -> string
        end
    and StringMacro = 
    /// Get value from message header
    |   Header of string
    /// Gets value from message body, for given xpath
    |   XPath  of string
    /// Gets a value, for the given custom function
    |   Func   of (Message -> string)
    /// Gets a literal value, constant string
    |   Literal  of string
    with
        member this.ToInt with get() = ConvertToInt(this)
        member this.ToFloat with get() = ConvertToFloat(this)
        member this.Substitute (message:Message) = (this :> ISubstitutable).Substitute message
        interface ISubstitutable with
            member this.Substitute (message:Message) =
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

    and IntegerMacro =
    /// Int constant
    |   Int of int
    /// Converts StringMacro output to int (string -> int), throws an exception if conversion fails
    |   ConvertToInt of StringMacro
    with
        member this.Substitute (message:Message) = (this :> ISubstitutable).Substitute message
        interface ISubstitutable with
            member this.Substitute (message:Message) =
                match this with
                |   Int value               -> sprintf "%d" value
                |   ConvertToInt strmacro   -> strmacro.Substitute(message)

    and FloatMacro =
    /// Float constant
    |   Float of float
    /// Converts StringMacro output to float (string -> float)
    |   ConvertToFloat of StringMacro
    with
        member this.Substitute (message:Message) = (this :> ISubstitutable).Substitute message
        interface ISubstitutable with
            member this.Substitute (message:Message) =
                match this with
                |   Float value             -> sprintf "%f" value
                |   ConvertToFloat strmacro -> strmacro.Substitute(message)
    and BooleanMacro =
    /// Bool constant
    |   Bool of bool
    /// Contains something that has to be evaluated with a message, and return a BooleanMacro
    |   Evaluatable of IComparison
    /// Inverse: b = !b
    |   Not of BooleanMacro
    /// Logical And: b = a && c
    |   And of BooleanMacro * BooleanMacro
    /// Logical Or: b = a || c
    |   Or of BooleanMacro * BooleanMacro
    with
        member this.Evaluate (message:Message) =
            match this with
            |   Bool value              -> value
            |   Evaluatable comparison  -> comparison.Evaluate(message).Evaluate message
            |   Not  value              -> not(value.Evaluate message)
            |   And  (l, r)             -> (l.Evaluate message) && (r.Evaluate message)
            |   Or  (l, r)              -> (l.Evaluate message) || (r.Evaluate message)

    /// Interface to expose "Evaluate" function, while hiding the Generic type parameter from the "Comparison" type.
    ///  We do this because we don't want to infect "BooleanMacro" with generic parameters.
    and IComparison =
        interface
            abstract member Evaluate : Message -> BooleanMacro
        end
    /// Compare two macros
    and Comparison<'T when 'T :> ISubstitutable> =
    /// Compares a = b
    |   Equals of 'T * 'T
    /// Compares a <> b
    |   Unequals of 'T * 'T
    /// Compares a < b
    |   LessThan of 'T * 'T
    /// Compares a > b
    |   GreaterThan of 'T * 'T
    with
        interface IComparison with
            member this.Evaluate (message) =
                match this with
                |   Equals (l,r) ->   Bool((l.Substitute message) = (r.Substitute message))
                |   Unequals (l,r) -> Bool((l.Substitute message) <> (r.Substitute message))
                |   LessThan (l,r) -> Bool((l.Substitute message) < (r.Substitute message))
                |   GreaterThan(l,r) -> Bool((l.Substitute message) > (r.Substitute message))


    type Operators = Operation with
        (* All allowed combination of Equals operator *)
        static member CompareEquals (Operation, l:StringMacro, r:string) = Equals(l,Literal(r))
        static member CompareEquals (Operation, l:StringMacro, r:StringMacro) = Equals(l,r)
        static member CompareEquals (Operation, l:IntegerMacro, r:int) = Equals(l,Int(r))
        static member CompareEquals (Operation, l:IntegerMacro, r:IntegerMacro) = Equals(l,r)
        static member CompareEquals (Operation, l:FloatMacro, r:float) = Equals(l,Float(r))
        static member CompareEquals (Operation, l:FloatMacro, r:FloatMacro) = Equals(l,r)

        (* All allowed combination of Unequals operator *)
        static member CompareUnEquals (Operation, l:StringMacro, r:string) = Unequals(l,Literal(r))
        static member CompareUnEquals (Operation, l:StringMacro, r:StringMacro) = Unequals(l,r)
        static member CompareUnEquals (Operation, l:IntegerMacro, r:int) = Unequals(l,Int(r))
        static member CompareUnEquals (Operation, l:IntegerMacro, r:IntegerMacro) = Unequals(l,r)
        static member CompareUnEquals (Operation, l:FloatMacro, r:float) = Unequals(l,Float(r))
        static member CompareUnEquals (Operation, l:FloatMacro, r:FloatMacro) = Unequals(l,r)

        (* All allowed combination of LT operator *)
        static member CompareLT (Operation, l:StringMacro, r:string) = LessThan(l,Literal(r))
        static member CompareLT (Operation, l:StringMacro, r:StringMacro) = LessThan(l,r)
        static member CompareLT (Operation, l:IntegerMacro, r:int) = LessThan(l,Int(r))
        static member CompareLT (Operation, l:IntegerMacro, r:IntegerMacro) = LessThan(l,r)
        static member CompareLT (Operation, l:FloatMacro, r:float) = LessThan(l,Float(r))
        static member CompareLT (Operation, l:FloatMacro, r:FloatMacro) = LessThan(l,r)

        (* All allowed combination of GT operator *)
        static member CompareGT (Operation, l:StringMacro, r:string) = GreaterThan(l,Literal(r))
        static member CompareGT (Operation, l:StringMacro, r:StringMacro) = GreaterThan(l,r)
        static member CompareGT (Operation, l:IntegerMacro, r:int) = GreaterThan(l,Int(r))
        static member CompareGT (Operation, l:IntegerMacro, r:IntegerMacro) = GreaterThan(l,r)
        static member CompareGT (Operation, l:FloatMacro, r:float) = GreaterThan(l,Float(r))
        static member CompareGT (Operation, l:FloatMacro, r:FloatMacro) = GreaterThan(l,r)


    /// Equals comparison
    let inline ( &= ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareEquals : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Unequals comparison
    let inline ( <&> ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareUnEquals : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Less than comparison
    let inline ( <& ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareLT : ^T * ^N * ^M -> _) (Operation, l, r))

    /// Greatre than comparison
    let inline ( &> ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareGT : ^T * ^N * ^M -> _) (Operation, l, r))

