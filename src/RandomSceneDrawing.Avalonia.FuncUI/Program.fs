namespace RandomSceneDrawing


open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open FluentAvalonia.Styling

open LibVLCSharp.Avalonia.FuncUI

open RandomSceneDrawing.AvaloniaExtensions

type App() =
    inherit Application()

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->

            let mainWindow = MainWindow FloatingContent.floating


            this.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
            let fluentAvaloniaTheme = FluentAvaloniaTheme(baseUri = null)
            this.Styles.Add fluentAvaloniaTheme
            fluentAvaloniaTheme.ForceWin32WindowToTheme mainWindow
            this.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

#if DEBUG
            mainWindow.AttachDevTools()
#endif
            desktopLifetime.MainWindow <- mainWindow

            let mainApi = Platform.mainApi mainWindow

            let settingsApi = Platform.settingsApi mainWindow
            let playerApi = Platform.playerApi mainWindow

            let mainPlayer = PlayerLib.LibVLCSharp.initPlayer
            let subPlayer = PlayerLib.LibVLCSharp.initSubPlayer

            let init  =
                Main.init settingsApi

            let update = Main.update mainApi settingsApi playerApi

            mainWindow.Content <- View.cmp mainPlayer subPlayer init update

        | _ -> ()

module Main =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
