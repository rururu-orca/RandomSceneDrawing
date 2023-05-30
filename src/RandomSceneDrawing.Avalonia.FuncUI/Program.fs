namespace RandomSceneDrawing

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open FluentAvalonia.Styling

open LibVLCSharp.Avalonia.FuncUI

open RandomSceneDrawing.AvaloniaExtensions

type App() =
    inherit Application()

    override this.OnFrameworkInitializationCompleted() =

        let lifetime =
            Application.Current.ApplicationLifetime :?> IClassicDesktopStyleApplicationLifetime

        let mainWindow = MainWindow View.mainPlayerFloating

        this.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))
        let fluentAvaloniaTheme = FluentAvaloniaTheme(baseUri = null)
        this.Styles.Add fluentAvaloniaTheme
        fluentAvaloniaTheme.ForceWin32WindowToTheme mainWindow
        this.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

#if DEBUG
        mainWindow.AttachDevTools()
#endif
        lifetime.MainWindow <- mainWindow

        let mainApi = Platform.mainApi mainWindow

        let settingsApi = Platform.settingsApi mainWindow
        let playerApi = Platform.playerApi mainWindow

        let initMainPlayer = PlayerLib.LibVLCSharp.initPlayer
        let initSubPlayer = PlayerLib.LibVLCSharp.initSubPlayer

        LibVLCSharp.Shared.Core.Initialize()
        let init =  Main.init settingsApi

        let update = Main.update mainApi settingsApi playerApi

        mainWindow.Content <- View.cmp initMainPlayer initSubPlayer init update


module Main =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .With( Win32PlatformOptions(
                UseWindowsUIComposition=true,
                OverlayPopups=true
            )).StartWithClassicDesktopLifetime(args)
