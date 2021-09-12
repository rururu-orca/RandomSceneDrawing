namespace RandomSceneDrawing

open System
open Avalonia.Controls
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
        | {RandomDrawingState = RandomDrawingState.Stop} -> true
        | _ -> false

    let videoViewContent model dispatch =
        Panel.create [
            Panel.children [
                Rectangle.create [
                    Rectangle.fill "black"
                    mediaBlindVisibility model |> Rectangle.isVisible
                ]
                DockPanel.create [
                    drawingSettingVisibility model |> DockPanel.isVisible
                    DockPanel.children [
                        StackPanel.create [
                            StackPanel.dock Dock.Top
                            StackPanel.margin 4.0
                            StackPanel.orientation Orientation.Horizontal
                            StackPanel.verticalAlignment VerticalAlignment.Top
                            StackPanel.isVisible true
                            StackPanel.children [
                                Button.create [
                                    Button.content "🔀 Show Random 🔀"
                                    Button.isEnabled (not (String.IsNullOrEmpty model.PlayListFilePath))
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
                            StackPanel.margin 4.0
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
                            StackPanel.margin 4.0
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
                StackPanel.create [
                    StackPanel.dock Dock.Top
                    StackPanel.spacing 4.0
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
                        TextBox.create [
                            TextBox.text (model.Duration.ToString @"hh\:mm\:ss")
                            TextBox.onLostFocus
                                (fun e ->
                                    match e.Source with
                                    | :? TextBox as t -> Some t
                                    | _ -> None
                                    |> Option.bind
                                        (fun t ->
                                            match TimeSpan.TryParse t.Text with
                                            | true, time -> Some time
                                            | _ -> None)
                                    |> Option.iter (SetDuration >> dispatch))
                            TextBox.onPointerWheelChanged
                                (fun e ->
                                    e.Delta.Y * 10.0
                                    |> TimeSpan.FromSeconds
                                    |> IncrementDuration
                                    |> dispatch)
                        ]
                        NumericUpDown.create [
                            NumericUpDown.minimum 1.0
                            NumericUpDown.value (double model.Frames)
                            NumericUpDown.onValueChanged (int >> SetFrames >> dispatch)
                        ]
                        TextBlock.create [
                            TextBlock.text (model.CurrentFrames.ToString())
                        ]
                        TextBlock.create [
                            TextBlock.text (model.CurrentDuration.ToString @"hh\:mm\:ss")
                        ]
                    ]
                ]
                VideoView.create [
                    VideoView.mediaPlayer model.Player
                    VideoView.content (videoViewContent model dispatch)
                ]
            ]
        ]
