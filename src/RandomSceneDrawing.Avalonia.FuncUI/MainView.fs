namespace RandomSceneDrawing

open System

open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Layout

open LibVLCSharp.Avalonia.FuncUI

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Threading
open FSharpPlus

open RandomSceneDrawing.Types
open RandomSceneDrawing.Util
open RandomSceneDrawing.AvaloniaExtensions

module MainView =
    let mediaBlindVisibility =
        function
        | { RandomDrawingState = Interval }
        | { RandomizeState = Running }
        | { RandomizeState = WaitBuffering }
        | { PlayerState = Stopped } -> true
        | { PlayerState = Playing }
        | { PlayerState = Paused } -> false

    let drawingSettingVisibility =
        function
        | { RandomDrawingState = RandomDrawingState.Stop } -> true
        | _ -> false

    let durationBox (model: Model) dispatch =
        let secs =
            [ 10.0
              30.0
              45.0
              60.0
              90.0
              120.0
              180.0
              300.0
              600.0
              1200.0
              1800.0 ]

        ComboBox.create [
            List.map TimeSpan.FromSeconds secs
            |> ComboBox.dataItems
            ComboBox.selectedItem model.Duration
            ComboBox.itemTemplate (
                DataTemplateView<TimeSpan>.create
                    (fun i ->
                        TextBlock.create [
                            TextBlock.text $"{i}"
                        ])
            )
            ComboBox.onPointerWheelChanged (fun e ->
                match e.Source with
                | :? ComboBox as combo ->
                    match combo.SelectedIndex + int e.Delta.Y with
                    | out when out < 0 || secs.Length < out -> ()
                    | newIndex -> combo.SelectedIndex <- newIndex
                | _ -> ())
            ComboBox.onSelectedItemChanged (fun item -> item :?> TimeSpan |> SetDuration |> dispatch)
        ]

    let subPlayerView model dispatch =
        let subViewwidth =
            if mediaBlindVisibility model |> not then
                config.SubPlayer.Width
            else
                0

        VideoView.create [
            VideoView.dock Dock.Right
            VideoView.height config.SubPlayer.Height
            VideoView.width subViewwidth
            VideoView.verticalAlignment VerticalAlignment.Top
            VideoView.horizontalAlignment HorizontalAlignment.Right
            VideoView.mediaPlayer model.SubPlayer
        ]

    let videoViewContent model dispatch =
        Panel.create [
            Panel.children [
                Rectangle.create [
                    Rectangle.classes [ "videoViewBlind" ]
                    mediaBlindVisibility model |> Rectangle.isVisible
                ]
                DockPanel.create [
                    DockPanel.children [
                        DockPanel.create [
                            DockPanel.dock Dock.Left
                            drawingSettingVisibility model
                            |> DockPanel.isVisible
                            DockPanel.children [
                                StackPanel.create [
                                    StackPanel.classes [ "floating" ]
                                    StackPanel.children [
                                        Button.create [
                                            Button.content "🔀 Show Random 🔀"
                                            (not (String.IsNullOrEmpty model.PlayListFilePath)
                                             && model.RandomizeState = Waiting)
                                            |> Button.isEnabled
                                            Button.onClick (fun _ -> dispatch RequestRandomize)
                                        ]
                                        Button.create [
                                            Button.content "Play"
                                            Button.onClick (fun _ -> dispatch RequestPlay)
                                        ]
                                        Button.create [
                                            match model.PlayerState with
                                            | Stopped ->
                                                Button.content "Pause"
                                                Button.isEnabled false
                                            | Playing ->
                                                Button.content "Pause"
                                                Button.onClick (fun _ -> dispatch RequestPause)
                                            | Paused ->
                                                Button.content "Resume"
                                                Button.onClick (fun _ -> dispatch RequestPause)
                                        ]
                                        Button.create [
                                            Button.content "Stop"
                                            Button.onClick (fun _ -> dispatch RequestStop)
                                            if model.PlayerState = Stopped then
                                                Button.isEnabled false
                                        ]
                                    ]
                                ]
                                StackPanel.create [
                                    StackPanel.classes [ "floating" ]
                                    StackPanel.children [
                                        Button.create [
                                            Button.content "PlayList"
                                            Button.onClick (fun _ -> dispatch RequestSelectPlayListFilePath)
                                        ]
                                        TextBox.create [
                                            TextBox.text model.PlayListFilePath
                                        ]
                                    ]
                                ]
                                StackPanel.create [
                                    StackPanel.classes [ "floating" ]
                                    StackPanel.children [
                                        Button.create [
                                            Button.content "SnapShotFolder"
                                            Button.onClick (fun _ -> dispatch RequestSelectSnapShotFolderPath)
                                        ]
                                        TextBox.create [
                                            TextBox.text model.SnapShotFolderPath
                                        ]
                                    ]
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]

    module ToolWindow =
        open LibVLCSharp.Shared

        let create model dispatch =
            SubWindow.create [
                SubWindow.isVisible (
                    model.RandomDrawingState
                    <> RandomDrawingState.Stop
                )
                SubWindow.dock Dock.Bottom
                SubWindow.content (
                    DockPanel.create [
                        DockPanel.margin 8
                        DockPanel.children [
                            Slider.create [
                                Slider.isEnabled model.Player.IsSeekable
                                Slider.dock Dock.Top
                                Slider.minimum 0.0
                                Slider.maximum 1.0
                                Slider.value (double model.Player.Position)
                                Slider.onValueChanged (fun e ->
                                    Dispatcher.UIThread.Post(fun _ -> model.Player.Position <- float32 e))
                            ]
                            StackPanel.create [
                                StackPanel.children [
                                    Button.create [
                                        Button.isEnabled model.Player.IsSeekable
                                        Button.content "Show Random"
                                        Button.onClick (fun _ -> dispatch RequestRandomize)
                                    ]

                                    Button.create [
                                        Button.isEnabled (
                                            model.Player.IsSeekable
                                            && model.Player.State <> VLCState.Buffering
                                        )
                                        Button.content "Next Frame"
                                        Button.onClick (fun _ ->
                                            Dispatcher.UIThread.Post(fun _ -> model.Player.NextFrame())
                                            Dispatcher.UIThread.Post id)
                                    ]
                                ]
                            ]
                        ]
                    ]
                )
            ]

    let view (model: Model) (dispatch: Msg -> unit) =
        DockPanel.create [
            DockPanel.children [
                ToolWindow.create model dispatch
                DockPanel.create [
                    DockPanel.dock Dock.Top
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.dock Dock.Left
                            StackPanel.orientation Orientation.Vertical
                            StackPanel.children [
                                StackPanel.create [
                                    StackPanel.children [
                                        Button.create [
                                            if model.RandomDrawingState = RandomDrawingState.Stop then
                                                Button.content "⏲ Start Drawing"
                                                Button.onClick (fun _ -> dispatch RequestStartDrawing)

                                                [ model.PlayListFilePath
                                                  model.SnapShotFolderPath ]
                                                |> List.forall (String.IsNullOrEmpty >> not)
                                                |> Button.isEnabled
                                            else
                                                Button.content "Stop Drawing"
                                                Button.onClick (fun _ -> dispatch RequestStopDrawing)
                                        ]
                                        match model.RandomDrawingState with
                                        | RandomDrawingState.Stop ->
                                            durationBox model dispatch

                                            NumericUpDown.create [
                                                NumericUpDown.minimum 1.0
                                                NumericUpDown.value (double model.Frames)
                                                NumericUpDown.onValueChanged (int >> SetFrames >> dispatch)
                                            ]
                                        | _ ->
                                            TextBlock.create [
                                                TextBlock.width 100.0
                                                TextBlock.text (model.CurrentFrames.ToString())
                                            ]

                                            TextBlock.create [
                                                TextBlock.text (model.CurrentDuration.ToString @"hh\:mm\:ss")
                                            ]
                                    ]
                                ]
                                StackPanel.create [
                                    StackPanel.horizontalAlignment HorizontalAlignment.Right
                                    StackPanel.children [
                                        match model.PlayerState with
                                        | Playing
                                        | Paused ->
                                            TextBlock.create [
                                                TextBlock.text model.Title
                                            ]

                                            TextBlock.create [
                                                TextBlock.text (model.MediaPosition.ToString @"hh\:mm\:ss")
                                            ]

                                            TextBlock.create [ TextBlock.text "/" ]

                                            TextBlock.create [
                                                TextBlock.text (model.MediaDuration.ToString @"hh\:mm\:ss")
                                            ]
                                        | _ -> ()
                                    ]
                                ]
                            ]
                        ]
                        subPlayerView model dispatch
                    ]
                ]
                ProgressBar.create [
                    ProgressBar.dock Dock.Top
                    (model.Duration - model.CurrentDuration)
                    / model.Duration
                    * 100.0
                    |> ProgressBar.value
                    match model.RandomDrawingState with
                    | RandomDrawingState.Running -> ProgressBar.isVisible true
                    | _ -> ProgressBar.isVisible false
                ]

                VideoView.create [
                    VideoView.mediaPlayer model.Player
                    VideoView.hasFloating true
                    VideoView.content (videoViewContent model dispatch)
                ]
            ]
        ]

open Avalonia.Controls.Notifications
open Avalonia.FuncUI.Hosts

type MainWindow(floatingWindow) =
    inherit HostWindow(Title = "Random Pause  動画のシーンがランダムで表示されます", Height = 720.0, Width = 1280.0)

    // Setup NotificationManager
    // To avoid the Airspace problem, host is configured with FloatingContent.floating.
    let notificationManager =
        WindowNotificationManager(floatingWindow, Position = NotificationPosition.BottomRight, MaxItems = 3)

    member _.NotificationManager = notificationManager
