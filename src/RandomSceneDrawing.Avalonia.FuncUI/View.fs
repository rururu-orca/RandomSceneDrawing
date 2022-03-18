module RandomSceneDrawing.View

open System

open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Layout

open LibVLCSharp.Avalonia.FuncUI

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.Threading

open FsToolkit.ErrorHandling

open RandomSceneDrawing.Types
open RandomSceneDrawing.Util
open RandomSceneDrawing.DrawingSettings
open RandomSceneDrawing.Player
open RandomSceneDrawing.Main
open RandomSceneDrawing.AvaloniaExtensions


let subPlayerView model =
    VideoView.create [
        VideoView.height config.SubPlayer.Height
        VideoView.width config.SubPlayer.Width
        VideoView.verticalAlignment VerticalAlignment.Top
        VideoView.horizontalAlignment HorizontalAlignment.Right
        match model.SubPlayer with
        | Resolved player ->
            VideoView.mediaPlayer player.Player

            Deferred.resolved player.Media
            |> VideoView.isVideoVisible
        | _ -> ()
    ]

let drawingSwtchBotton model dispatch =
    Button.create [
        let s = model.Settings.Settings

        match model.State with
        | Setting ->
            Button.content "⏲ Start Drawing"
            Button.onClick (fun _ -> StartDrawing Started |> dispatch)

            match s.PlayListFilePath, s.SnapShotFolderPath with
            | Valid _, Valid _ -> true
            | _ -> false
            |> Button.isEnabled
        | _ ->
            Button.content "Stop Drawing"
            Button.onClick (fun _ -> StopDrawing |> dispatch)
    ]


let headerView model dispatch =
    DockPanel.create [
        DockPanel.dock Dock.Top
        DockPanel.children [
            StackPanel.create [
                StackPanel.dock Dock.Left
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    drawingSwtchBotton model dispatch
                    Button.create [
                        Button.content "Play"
                        Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Play Started)) |> dispatch)
                    ]
                    Button.create [
                        Button.content "Stop"
                        Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Stop Started)) |> dispatch)
                    ]
                ]
            ]
            subPlayerView model
        ]
    ]

let floatingContent model dispatch =
    Panel.create [
        Panel.children [
            Rectangle.create [
                Rectangle.classes [ "videoViewBlind" ]
                match model.MainPlayer with
                | Resolved mainPlayer ->
                    mainPlayer.Media
                    |> Deferred.exists Result.isError
                    |> Rectangle.isVisible
                | _ -> ()
            ]
        ]
    ]

let mainPlayerView model dispatch =
    VideoView.create [
        VideoView.hasFloating true
        floatingContent model dispatch
        |> VideoView.content
        match model.MainPlayer with
        | Resolved mainPlayer ->
            VideoView.mediaPlayer mainPlayer.Player
            VideoView.isVideoVisible mainPlayer.Player.IsSeekable
        | _ -> ()
    ]

let view (model: Model<'player>) dispatch =
    DockPanel.create [
        DockPanel.children [
            headerView model dispatch
            mainPlayerView model dispatch
        ]
    ]
