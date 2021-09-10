namespace RandomSceneDrawing

open System
open FSharpPlus
open LibVLCSharp.Shared
open LibVLCSharp.Avalonia.FuncUI

// Define a function to construct a message to print
open Avalonia.FuncUI.DSL

open Elmish
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Avalonia.Media
open RandomSceneDrawing.Types
open RandomSceneDrawing.Program


module ProgramUtil =
    let mkProgramWithCmdMsg
        (init: unit -> 'model * 'cmdMsg list)
        (update: 'msg -> 'model -> 'model * 'cmdMsg list)
        (view: 'model -> Dispatch<'msg> -> 'view)
        (toCmd: 'cmdMsg -> Cmd<'msg>)
        =
        let convert (model, cmdMsgs) =
            model, (cmdMsgs |> List.map toCmd |> Cmd.batch)

        Program.mkProgram (init >> convert) (fun msg model -> update msg model |> convert) view


type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Counter Example"
        base.Height <- 400.0
        base.Width <- 400.0

        Core.Initialize()

        let toCmd  =
            function
            | Play player -> Cmd.none 
            | _ -> Cmd.none

        //this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        ProgramUtil.mkProgramWithCmdMsg Program.init Program.update MainView.view toCmd
        |> Program.withHost this
        |> Program.withConsoleTrace
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            let mainWindow = MainWindow()
            desktopLifetime.MainWindow <- mainWindow
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
