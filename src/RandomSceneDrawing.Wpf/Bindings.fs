module RandomSceneDrawing.Bindings

open System.Dynamic
open FSharp.Interop.Dynamic
open System.Windows.Input
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open Microsoft.FSharp.Reflection
open Elmish
open Elmish.WPF
open RandomSceneDrawing.Types

type VmBinding = Vm of obj * (string -> Binding<Model, Msg>)


module VmBindings =
    let ToBindings x =
        [ for p in FSharpType.GetRecordFields(x.GetType()) -> p.Name, p.GetValue(x) :?> VmBinding]
        |> Seq.map (fun (name, Vm (_, binding)) -> name |> binding)

    let ToDesignerInstance x =
        let expando = ExpandoObject()

        [ for p in FSharpType.GetRecordFields(x.GetType()) -> p.Name, p.GetValue(x) :?> VmBinding ]
        |> Seq.map
            (function
            | (name, Vm (v, _)) -> name, v)
        |> Seq.fold
            (fun state (n, v) ->
                Dyn.set n v state
                state)
            expando