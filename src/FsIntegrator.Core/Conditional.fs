namespace FsIntegrator


type Conditional = ConditionOperation with
    /// Specifies a conditional route
    static member When (ConditionOperation, condition : BooleanMacro) = ConditionalRoute.Create [] condition

    /// Specifies a conditional route
    static member When (ConditionOperation, condition : IComparison) = ConditionalRoute.Create [] (Evaluatable(condition))

