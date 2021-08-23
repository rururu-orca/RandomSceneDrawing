﻿module RandomSceneDrawing.App

open System
open System.Reflection
open System.Windows
open FSharp.Control.Reactive
open Elmish
open Elmish.WPF
open Serilog
open Serilog.Extensions.Logging

let startMainLoop window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
#if DEBUG
            .WriteTo
            .Console()
#endif
            .CreateLogger()

    let cmds =
        Interop.WindowInteropHelper(window).Handle
        |> Program.toCmd

    WpfProgram.mkProgramWithCmdMsg Program.init Program.update Program.bindings cmds
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.withSubscription
        (fun _ ->
            Cmd.batch [ Cmd.ofSub DrawingSetvice.setup
                        Cmd.ofSub PlayerLib.timeChanged
                        Cmd.ofSub PlayerLib.playerBuffering ])
    |> WpfProgram.startElmishLoop window

[<STAThread>]
[<EntryPoint>]
let main argv =
    Assembly.Load "Microsoft.Xaml.Behaviors" |> ignore

    let application =
        Application.LoadComponent
        <| Uri("App.xaml", UriKind.Relative)
        :?> Application

    Observable.first application.Activated
    |> Observable.add (fun _ -> startMainLoop application.MainWindow)

    application.Run()
