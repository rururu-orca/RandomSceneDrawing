namespace RandomSceneDrawing

open Avalonia.Controls.Notifications
open Avalonia.FuncUI.Hosts

type MainWindow(floatingWindow) =
    inherit HostWindow(Title = "Random Pause  動画のシーンがランダムで表示されます", Height = 908.0, Width = 1280.0)

    // Setup NotificationManager
    // To avoid the Airspace problem, host is configured with FloatingContent.floating.
    let notificationManager =
        if isNull floatingWindow then
            invalidArg "floatingWindow" "Null Window!!"

        WindowNotificationManager(floatingWindow, Position = NotificationPosition.BottomRight, MaxItems = 3)

    member _.NotificationManager = notificationManager
