namespace FsIntegrator

open FsIntegrator.Core
open FsIntegrator.Core.MessageOperations


module Conditionals =

    type Conditional = ConditionOperation with
        /// Specifies a conditional route
        static member When (ConditionOperation, condition : BooleanMacro) = ConditionalRoute.Create [] condition

        /// Specifies a conditional route
        static member When (ConditionOperation, condition : IComparison) = ConditionalRoute.Create [] (Evaluatable(condition))


    /// Specifies a conditional route
    let inline When (condition:'N) = ((^T or ^N) : (static member When : ^T * ^N -> _) (ConditionOperation, condition))

    /// Will always be executed if all others are false
    let inline Otherwise() = ConditionalRoute.Create [] (Bool(true))

