// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module RandomSceneDrawing.App

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Markup
open FSharp.Control.Reactive
open LibVLCSharp.WPF
open LibVLCSharp.Shared

[<STAThread>]
[<EntryPoint>]
let main argv =
    let application =
        Application.LoadComponent <| Uri("App.xaml", UriKind.Relative)
        :?> Application
    Core.Initialize()

    Observable.first application.Activated
    |> Observable.add (fun _ -> Program.main application.MainWindow)


    application.Run()