namespace RandomSceneDrawing

open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Builder
open RandomSceneDrawing.Types
open RandomSceneDrawing.Program
open LibVLCSharp.Avalonia.FuncUI

module MainView =
    let view (model:Model) dispatch =
        DockPanel.create [
            DockPanel.children [
                VideoView.create [VideoView.mediaPlayer model.Player]
            ]
        ]