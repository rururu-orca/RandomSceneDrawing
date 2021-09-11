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
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open FluentAvalonia.Styling


module Program =
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
    inherit HostWindow(Title = "Random Pause  動画のシーンがランダムで表示されます", Height = 720.0, Width = 1280.0)

    do
        // Setup LibVLC
        Core.Initialize()


#if DEBUG
        this.AttachDevTools()
#endif

        // Start mainloop
        Program.mkProgramWithCmdMsg Program.init Program.update MainView.view (Platform.toCmd this)
        |> Program.withHost this
        |> Program.withSubscription Platform.subs
        |> Program.withConsoleTrace
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =

        // Apply Fluent Theme
        this.Styles.Add (FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime -> desktopLifetime.MainWindow <- MainWindow()
        | _ -> ()

module Main =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
