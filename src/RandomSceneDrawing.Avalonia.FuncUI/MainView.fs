namespace RandomSceneDrawing

open Avalonia.Controls.Notifications
open Avalonia.FuncUI.Hosts
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

type MainWindow(floatingWindow) =
    inherit HostWindow
        (
            Title = "Random Pause  動画のシーンがランダムで表示されます",
            Height = MainWindowConfig.height,
            Width = MainWindowConfig.width,
            MinHeight = MainWindowConfig.height,
            MinWidth = MainWindowConfig.width
        )

    // Setup NotificationManager
    // To avoid the Airspace problem, host is configured with FloatingContent.floating.
    let notificationManager =
        if isNull floatingWindow then
            invalidArg "floatingWindow" "Null Window!!"

        WindowNotificationManager(floatingWindow, Position = NotificationPosition.BottomRight, MaxItems = 3)

    member _.NotificationManager = notificationManager
