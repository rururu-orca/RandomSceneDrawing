module RandomSceneDrawing.Tests.Bindings

open System
open Expecto
open RandomSceneDrawing
open Microsoft.FSharp.Reflection
open FSharp.Interop.Dynamic

module TestVm =
    open RandomSceneDrawing.Bindings
    open Elmish.WPF

    let testVm () =
        {| TestText = Vm("Test", Binding.oneWay (fun m -> m.Title)) |}

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
