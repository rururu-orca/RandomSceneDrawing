module RandomSceneDrawing.Tests.Bindings

open System
open Expecto
open RandomSceneDrawing
open FSharp.Interop.Dynamic

module TestVm =
    open RandomSceneDrawing.Bindings
    open Elmish.WPF

    let testVm () =
        {| TestText = Vm("Test", Binding.oneWay (fun m -> m.Title)) |}

[<Tests>]
let bindingsTests =

    testList "VmBinding"
    <| [ test "can map Binding<Model, Msg>" {
             let actual =
                 TestVm.testVm () |> Bindings.VmBindings.ToBindings

             let head = Seq.head actual
             Expect.equal head?Name "TestText" ""

             Expect.equal (Seq.length actual) 1 ""
         }
         test "can map DesignerInstance" {
             let actual =
                 TestVm.testVm ()
                 |> Bindings.VmBindings.ToDesignerInstance

             Expect.equal actual?TestText "Test" ""
         } ]
