namespace RandomSceneDrawing


open Avalonia.Controls
open Avalonia.Controls.Notifications
open Util

module MainWindowConfig =
    let height =
        float (
            config.MainPlayer.Height
            + config.SubPlayer.Height
            + config.RootComponent.Margin * 3
        )

    let width =
        float (
            config.MainPlayer.Width
            + config.RootComponent.Margin * 2
        )

type MainWindow(floatingName)  =
    inherit Window
        (
            new FloatingWindowOwnerImpl(),
            Title = "Random Pause  動画のシーンがランダムで表示されます",
            Height = MainWindowConfig.height,
            Width = MainWindowConfig.width,
            MinHeight = MainWindowConfig.height,
            MinWidth = MainWindowConfig.width
        )

    let floating = lazy (FloatingWindow.TryGet floatingName |> Option.get)

    let initWindowNotificationManager window =
        WindowNotificationManager(window, Position = NotificationPosition.BottomRight, MaxItems = 3)

    // Setup NotificationManager
    // To avoid the Airspace problem, host is configured with FloatingContent.floating.
    let floatingManager = lazy initWindowNotificationManager floating.Value

    member x.NotificationManager =
        floatingManager.Value
        