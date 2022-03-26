namespace RandomSceneDrawing


open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open FluentAvalonia.Styling

open LibVLCSharp.Avalonia.FuncUI

open RandomSceneDrawing.AvaloniaExtensions

type App() =
    inherit Application()

    let applyFluentTheme (app: App) mainWindow =
        let fluentAvaloniaTheme = FluentAvaloniaTheme(baseUri = null)

        app.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
        app.Styles.Add fluentAvaloniaTheme
        fluentAvaloniaTheme.ForceWin32WindowToTheme mainWindow

    /// Core Logic
    let run (app: App) (desktopLifetime: IClassicDesktopStyleApplicationLifetime) =

        let mainWindow = MainWindow FloatingContent.floating
#if DEBUG
        mainWindow.AttachDevTools()
#endif
        desktopLifetime.MainWindow <- mainWindow

        applyFluentTheme app mainWindow
        app.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

        let mainApi =
            Platform.mainApi mainWindow

        let settingsApi = Platform.settingsApi mainWindow
        let playerApi = Platform.playerApi mainWindow

        let mainPlayer = PlayerLib.LibVLCSharp.initPlayer
        let subPlayer = PlayerLib.LibVLCSharp.initSubPlayer
        let init () = Main.init mainPlayer subPlayer settingsApi mainWindow.Closed

        let update = Main.update mainApi settingsApi playerApi

        mainWindow.Content <- View.cmp (init()) update


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
