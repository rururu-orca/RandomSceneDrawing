// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp
module RandomSceneDrawing.App

open System
open System.Reflection
open System.Windows
open FSharp.Control.Reactive

[<STAThread>]
[<EntryPoint>]
let main argv =
    Assembly.Load "Microsoft.Xaml.Behaviors" |> ignore
    
    let application =
        Application.LoadComponent
        <| Uri("App.xaml", UriKind.Relative)
        :?> Application


    Observable.first application.Activated
    |> Observable.add (fun _ -> Program.main application.MainWindow)

    application.Run()
