namespace FsIntegrator


/// FsIntegrator.Core holds any values that require a placeholder to store.

#nowarn "0064"
module Core =

    // ===========  Values related to FileSystemMonad.fsi ===========  

    let fs = FsIntegrator.FSBuilder() // FSBuilder()

    /// Empty File Script (do nothing)
    let NoFileScript = fun _ -> FSScript.Empty

    let inline (=>=) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member FlowOperator : ^T * ^N * ^M -> _) (Operation, l, r))


    // ===========  Values related to Macro.fs ===========  

   /// Equals comparison
    let inline ( &= ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareEquals : ^T * ^N * ^M -> _) (CompareOperation, l, r))

    /// Unequals comparison
    let inline ( <&> ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareUnEquals : ^T * ^N * ^M -> _) (CompareOperation, l, r))

    /// Less than comparison
    let inline ( <& ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareLT : ^T * ^N * ^M -> _) (CompareOperation, l, r))

    /// Greatre than comparison
    let inline ( &> ) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member CompareGT : ^T * ^N * ^M -> _) (CompareOperation, l, r))


    // ===========  Values related to Conditional.fs ===========  

    /// Specifies a conditional route
    let inline When (condition:'N) = ((^T or ^N) : (static member When : ^T * ^N -> _) (ConditionOperation, condition))

    /// Will always be executed if all others are false
    let inline Otherwise() = ConditionalRoute.Create [] (Bool(true))