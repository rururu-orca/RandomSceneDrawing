namespace RandomSceneDrawing


open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open FluentAvalonia.Styling

open Elmish
open Avalonia.FuncUI.Elmish

open FSharpPlus

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

type App() =
    inherit Application()

    let applyFluentTheme (app: App) mainWindow =
        let fluentAvaloniaTheme = FluentAvaloniaTheme(baseUri = null)
        app.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
        app.Styles.Add fluentAvaloniaTheme
        fluentAvaloniaTheme.ForceNativeTitleBarToTheme mainWindow

    let startMainLoop (mainWindow: MainWindow) =
        Program.mkProgramWithCmdMsg Program.init Program.update MainView.view (Platform.toCmd mainWindow)
        |> Program.withHost mainWindow
        |> Program.withSubscription (Platform.subs mainWindow)
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.run

    let run (app: App) (desktopLifetime: IClassicDesktopStyleApplicationLifetime) =
        LibVLCSharp.Shared.Core.Initialize()

        let mainWindow = MainWindow FloatingContent.floating
#if DEBUG
        mainWindow.AttachDevTools()
#endif
        desktopLifetime.MainWindow <- mainWindow

        applyFluentTheme app mainWindow
        app.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

        startMainLoop mainWindow

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime -> run this desktopLifetime
        | _ -> ()

module Main =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
