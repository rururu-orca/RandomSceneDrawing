namespace RandomSceneDrawing

open System
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Controls.Shapes
open Avalonia.Layout
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Components
open Avalonia.FuncUI.Elmish
open RandomSceneDrawing.Types
open RandomSceneDrawing.Program
open LibVLCSharp.Avalonia.FuncUI
open FSharpPlus


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
            ComboBox.onPointerWheelChanged
                (fun e ->
                    match e.Source with
                    | :? ComboBox as combo ->
                        match combo.SelectedIndex + int e.Delta.Y with
                        | out when out < 0 || secs.Length < out -> ()
                        | newIndex -> combo.SelectedIndex <- newIndex
                    | _ -> ())
            ComboBox.onSelectedItemChanged (fun item -> item :?> TimeSpan |> SetDuration |> dispatch)
        ]

    let videoViewContent model dispatch =
        Panel.create [
            Panel.children [
                Rectangle.create [
                    Rectangle.fill "black"
                    mediaBlindVisibility model |> Rectangle.isVisible
                ]
                DockPanel.create [
                    drawingSettingVisibility model
                    |> DockPanel.isVisible
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.dock Dock.Top
                            StackPanel.spacing 20.0
                            StackPanel.margin (0.0, 8.0, 0.0, 0.0)
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.verticalAlignment VerticalAlignment.Top
                            StackPanel.isVisible true
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
                                    Button.content "Pause"
                                    Button.onClick (fun _ -> dispatch RequestPause)
                                ]
                                Button.create [
                                    Button.content "Stop"
                                    Button.onClick (fun _ -> dispatch RequestStop)
                                ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.dock Dock.Top
                            StackPanel.margin (0.0, 8.0, 0.0, 0.0)
                            StackPanel.spacing 20.0
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.verticalAlignment VerticalAlignment.Top
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
                            StackPanel.dock Dock.Top
                            StackPanel.margin (0.0, 8.0, 0.0, 0.0)
                            StackPanel.spacing 20.0
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.verticalAlignment VerticalAlignment.Top
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

    let view (model: Model) (dispatch: Msg -> unit) =
        DockPanel.create [
            DockPanel.margin 4.0
            DockPanel.children [
                DockPanel.create [
                    DockPanel.dock Dock.Top
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.dock Dock.Left
                            StackPanel.spacing 20.0
                            StackPanel.orientation Orientation.Horizontal
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
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text (model.CurrentFrames.ToString())
                                    ]

                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text (model.CurrentDuration.ToString @"hh\:mm\:ss")
                                    ]
                            ]
                        ]
                        StackPanel.create [
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.spacing 20.0
                            StackPanel.horizontalAlignment HorizontalAlignment.Right
                            StackPanel.dock Dock.Right
                            StackPanel.children [
                                match model.PlayerState with
                                | Playing
                                | Paused ->
                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text model.Title
                                    ]

                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text (model.MediaPosition.ToString @"hh\:mm\:ss")
                                    ]

                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text "/"
                                    ]

                                    TextBlock.create [
                                        TextBlock.verticalAlignment VerticalAlignment.Center
                                        TextBlock.textAlignment TextAlignment.Center
                                        TextBlock.text (model.MediaDuration.ToString @"hh\:mm\:ss")
                                    ]
                                | _ -> ()
                            ]
                        ]
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
                    VideoView.content (videoViewContent model dispatch)
                ]
            ]
        ]
