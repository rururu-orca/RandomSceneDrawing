namespace RandomSceneDrawing

open System
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent

open LibVLCSharp.Avalonia.FuncUI

open RandomSceneDrawing.AvaloniaExtensions

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        this.RequestedThemeVariant <- Styling.ThemeVariant.Dark
        this.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Styles/Styles.xaml"

    override _.OnFrameworkInitializationCompleted() =

        let lifetime =
            Application.Current.ApplicationLifetime :?> IClassicDesktopStyleApplicationLifetime

        let mainWindow = MainWindow View.mainPlayerFloating



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
        let init = Main.init settingsApi

        let update = Main.update mainApi settingsApi playerApi

        mainWindow.Content <- View.cmp initMainPlayer initSubPlayer init update


module Main =

    [<EntryPoint>]
    let main (args: string[]) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .With(Win32PlatformOptions(UseWindowsUIComposition = true, OverlayPopups = true))
            .StartWithClassicDesktopLifetime(args)
