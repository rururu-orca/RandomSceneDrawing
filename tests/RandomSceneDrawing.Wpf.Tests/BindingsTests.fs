module RandomSceneDrawing.Tests.Bindings

open Expecto
open RandomSceneDrawing
open Microsoft.FSharp.Reflection

[<Tests>]
let bindingsTests =

    testList "VmBinding"
    <| [ test "BindingLabel and DesignVm should be consistent." {
             let labels =
                 FSharpType.GetUnionCases(typeof<Bindings.BindingLabel>)
                 |> Seq.map (fun c -> c.Name)

             let names =
                 FSharpType.GetRecordFields(typeof<Bindings.DesignVm>)
                 |> Seq.map (fun c -> c.Name)

             Expect.sequenceEqual labels names "Case labels in BindingLabel and field names in DesignVm should match."
         } ]
