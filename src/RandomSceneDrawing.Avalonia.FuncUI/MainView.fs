namespace RandomSceneDrawing

open Avalonia.Controls
open Avalonia.Layout
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Builder
open RandomSceneDrawing.Types
open RandomSceneDrawing.Program
open LibVLCSharp.Avalonia.FuncUI

module MainView =

    let view (model: Model) (dispatch: Msg -> unit) =
        DockPanel.create [
            DockPanel.margin 4.0
            DockPanel.children [
                VideoView.create [
                    VideoView.mediaPlayer model.Player
                    VideoView.content (
                        DockPanel.create [
                            DockPanel.children [
                                StackPanel.create [
                                    StackPanel.dock Dock.Top
                                    StackPanel.orientation Orientation.Horizontal
                                    StackPanel.verticalAlignment VerticalAlignment.Top
                                    StackPanel.isVisible true
                                    StackPanel.children [
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
                            ]
                        ]
                    )
                ]
            ]
        ]
