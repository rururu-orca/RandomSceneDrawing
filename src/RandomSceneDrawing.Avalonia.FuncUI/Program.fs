namespace RandomSceneDrawing


open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open FluentAvalonia.Styling

open Elmish
open Avalonia.FuncUI.Elmish

open LibVLCSharp.Avalonia.FuncUI

open RandomSceneDrawing.AvaloniaExtensions

type App() =
    inherit Application()

    let applyFluentTheme (app: App) mainWindow =
        let fluentAvaloniaTheme = FluentAvaloniaTheme(baseUri = null)

        app.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
        app.Styles.Add fluentAvaloniaTheme
        fluentAvaloniaTheme.ForceWin32WindowToTheme mainWindow

    let startMainLoop (mainWindow: MainWindow) =
        Program.mkProgram Program.init (Platform.api mainWindow |> Program.updateProto) MainView.view
        |> Program.withHost mainWindow
        |> Program.withSubscription (Platform.subs mainWindow)
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.run

    /// Core Logic
    let run (app: App) (desktopLifetime: IClassicDesktopStyleApplicationLifetime) =
        LibVLCSharp.Core.Initialize()

        let mainWindow = MainWindow FloatingContent.floating
#if DEBUG
        mainWindow.AttachDevTools()
#endif
        desktopLifetime.MainWindow <- mainWindow

        applyFluentTheme app mainWindow
        app.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

        // startMainLoop mainWindow
        let mainPlayer = PlayerLib.initPlayer
        let subPlayer = PlayerLib.initSubPlayer
        let init () = Main.init mainPlayer subPlayer mainWindow.Closed

        let mainApi =
            Platform.mainApi mainWindow

        let settingsApi = Platform.settingsApi
        let playerApi = Platform.playerApi
        let update = Main.update mainApi settingsApi playerApi

        Program.mkProgram init update View.view
        |> Program.withHost mainWindow
#if DEBUG
        |> Program.withConsoleTrace
#endif
        |> Program.run



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
