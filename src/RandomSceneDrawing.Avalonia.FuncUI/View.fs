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
open RandomSceneDrawing.Main
open RandomSceneDrawing.AvaloniaExtensions

let headerView model dispatch =
    Button.create [
        DockPanel.dock Dock.Top
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
        // if model.RandomDrawingState = RandomDrawingState.Stop then
        //     Button.content "⏲ Start Drawing"
        //     Button.onClick (fun _ -> dispatch RequestStartDrawing)

        //     [ model.PlayListFilePath
        //       model.SnapShotFolderPath ]
        //     |> List.forall (String.IsNullOrEmpty >> not)
        //     |> Button.isEnabled
        // else
        //     Button.content "Stop Drawing"
        //     Button.onClick (fun _ -> dispatch RequestStopDrawing)
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
