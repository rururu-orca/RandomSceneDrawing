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

let inline notFunc ([<InlineIfLambda>] f) x = not (f x)

let inline isNotInterval state =
    match state with
    | Interval _ -> false
    | _ -> true

let inline isNotRandomizeInProgress model =
    notFunc Deferred.inProgress model.RandomizeState

let inline isMediaResolved player =
    match player with
    | Resolved p -> Deferred.resolved p.Media
    | _ -> false


let subPlayerView model =
    VideoView.create [
        VideoView.height config.SubPlayer.Height
        VideoView.width config.SubPlayer.Width
        VideoView.verticalAlignment VerticalAlignment.Top
        VideoView.horizontalAlignment HorizontalAlignment.Right
        match model.SubPlayer with
        | Resolved player ->
            VideoView.mediaPlayer player.Player

            (Deferred.resolved player.Media
             && isNotInterval model.State
             && isNotRandomizeInProgress model)
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

let randomizeButton model dispatch =
    let settings = model.Settings.Settings

    Button.create [
        Button.content "🔀 Show Random 🔀"
        (ValueTypes.playListFilePath.IsValid settings.PlayListFilePath
         && notFunc Deferred.inProgress model.RandomizeState)
        |> Button.isEnabled
        Button.onClick (fun _ -> Randomize Started |> dispatch)
    ]

let mediaPlayerControler model dispatch =

    StackPanel.create [
        StackPanel.children [
            randomizeButton model dispatch
            Button.create [
                Button.content "Play"
                Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Play Started)) |> dispatch)
            ]
            Button.create [
                Button.content "Pause"
                isMediaResolved model.MainPlayer |> Button.isEnabled
                Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Pause Started)) |> dispatch)
            ]
            Button.create [
                Button.content "Stop"
                isMediaResolved model.MainPlayer |> Button.isEnabled
                Button.onClick (fun _ ->
                    PlayerMsg(MainPlayer, (Stop Started)) |> dispatch
                    PlayerMsg(SubPlayer, (Stop Started)) |> dispatch)
            ]
        ]
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
                    mediaPlayerControler model dispatch
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

        match model.MainPlayer with
        | Resolved mainPlayer ->
            VideoView.mediaPlayer mainPlayer.Player

            (Deferred.resolved mainPlayer.Media
             && isNotInterval model.State
             && isNotRandomizeInProgress model)
            |> VideoView.isVideoVisible
        | _ -> ()

        VideoView.hasFloating true
        floatingContent model dispatch
        |> VideoView.content
    ]

let view (model: Model<'player>) dispatch =
    DockPanel.create [
        DockPanel.children [
            headerView model dispatch
            mainPlayerView model dispatch
        ]
    ]
