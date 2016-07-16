namespace FsIntegrator


/// FsIntegrator.Core holds any values that require a placeholder to store.

open FsIntegrator

#nowarn "0064"
module Core =
    let fs = FsIntegrator.FSBuilder() // FSBuilder()

    /// Empty File Script (do nothing)
    let NoFileScript = fun _ -> FSScript.Empty

    let inline (=>=) (l:'N) (r:'M) = ((^T or ^N or ^M) : (static member FlowOperator : ^T * ^N * ^M -> _) (Operation, l, r))
