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

type MainWindow(floating:Window)  =
    inherit Window
        (
            new FloatingWindowOwnerImpl(),
            Title = "Random Pause  動画のシーンがランダムで表示されます",
            Height = MainWindowConfig.height,
            Width = MainWindowConfig.width,
            MinHeight = MainWindowConfig.height,
            MinWidth = MainWindowConfig.width
        )

    let notificationManager =
        WindowNotificationManager(floating, Position = NotificationPosition.BottomRight, MaxItems = 3)

    member x.NotificationManager = notificationManager
        
        