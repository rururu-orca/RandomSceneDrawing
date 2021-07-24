// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module RandomSceneDrawing.App

open System
open System.Reflection
open System.Windows
open FSharp.Control.Reactive

[<STAThread>]
[<EntryPoint>]
let main argv =
    let application =
        Application.LoadComponent
        <| Uri("App.xaml", UriKind.Relative)
        :?> Application

    Assembly.Load "Microsoft.Xaml.Behaviors" |> ignore

    Observable.first application.Activated
    |> Observable.add (fun _ -> Program.main application.MainWindow)

    application.Run()
