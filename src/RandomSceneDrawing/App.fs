// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module RandomSceneDrawing.App

open System
open System.Windows
open System.Windows.Controls
open System.Windows.Markup
open FSharp.Control.Reactive


[<STAThread>]
[<EntryPoint>]
let main argv =
    let application =
        Application.LoadComponent <| Uri("App.xaml", UriKind.Relative)
        :?> Application

    application.Run()