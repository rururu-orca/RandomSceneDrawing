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
open RandomSceneDrawing.Main.ValueTypes
open RandomSceneDrawing.AvaloniaExtensions


let inline list fsCollection =
    fsCollection :> Collections.Generic.IEnumerable<'T>

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

let validatedTextBox (domain: Domain<string, _, _>) value addAttrs dispatchSetValueMsg =
    let invalidTextAttrs text errors =
        [ TextBox.text text
          List.map box errors |> TextBox.errors ]

    TextBox.create [
        match value with
        | Valid v -> domain.ToDto v |> TextBox.text
        | Invalid (CreateFailed (dto, errors)) -> yield! invalidTextAttrs dto errors
        | Invalid (UpdateFailed (ValueNone, dto, errors)) -> yield! invalidTextAttrs dto errors
        | Invalid (UpdateFailed (ValueSome before, dto, errors)) ->
            yield! invalidTextAttrs dto errors
            TextBox.onLostFocus (fun _ -> (domain.ToDto >> dispatchSetValueMsg) before)
        | Invalid (MargedError marged) -> List.map box marged |> TextBox.errors

        yield! addAttrs

        TextBox.onTextChanged dispatchSetValueMsg
    ]

let subPlayerView model =
    VideoView.create [
        VideoView.height config.SubPlayer.Height
        VideoView.width config.SubPlayer.Width
        VideoView.margin (Avalonia.Thickness(4,4,0,4))
        VideoView.dock Dock.Right
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
        (playListFilePath.IsValid settings.PlayListFilePath
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
                notFunc Deferred.inProgress model.RandomizeState
                |> Button.isEnabled
                Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Play Started)) |> dispatch)
            ]
            Button.create [
                Button.content "Pause"
                isMediaResolved model.MainPlayer
                |> Button.isEnabled
                Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Pause Started)) |> dispatch)
            ]
            Button.create [
                Button.content "Stop"
                isMediaResolved model.MainPlayer
                |> Button.isEnabled
                Button.onClick (fun _ ->
                    PlayerMsg(MainPlayer, (Stop Started)) |> dispatch
                    PlayerMsg(SubPlayer, (Stop Started)) |> dispatch)
            ]
        ]
    ]



let playListFilePathView model dispatch =
    let settings = model.Settings.Settings

    let dispatchSetValueMsg s =
        (SetPlayListFilePath >> SettingsMsg) s |> dispatch

    Grid.create [
        Grid.rowDefinitions "*"
        Grid.columnDefinitions "Auto,*"
        Grid.column 0
        Grid.children [
            Button.create [
                StackPanel.column 0
                Button.content "PlayList"
                Button.onClick (fun _ -> (PickPlayList >> SettingsMsg) Started |> dispatch)
            ]
            validatedTextBox playListFilePath settings.PlayListFilePath [ StackPanel.column 1 ] dispatchSetValueMsg
        ]
    ]

let snapShotFolderPathView model dispatch =
    let settings = model.Settings.Settings

    let dispatchSetValueMsg s =
        (SetSnapShotFolderPath >> SettingsMsg) s
        |> dispatch

    Grid.create [
        Grid.rowDefinitions "*"
        Grid.columnDefinitions "Auto,*"
        Grid.column 1
        Grid.children [
            Button.create [
                StackPanel.column 0
                Button.content "SnapShotFolder"
                Button.onClick (fun _ ->
                    (PickSnapshotFolder >> SettingsMsg) Started
                    |> dispatch)
            ]
            validatedTextBox snapShotFolderPath settings.SnapShotFolderPath [ StackPanel.column 1 ] dispatchSetValueMsg
        ]
    ]


let pathSettings model dispatch =
    Grid.create [
        Grid.rowDefinitions "*"
        Grid.columnDefinitions "*,*"
        Grid.children [
            playListFilePathView model dispatch
            snapShotFolderPathView model dispatch
        ]
    ]


let headerView model dispatch =
    DockPanel.create [
        DockPanel.margin (Avalonia.Thickness(4,0,0,0))
        DockPanel.dock Dock.Top
        DockPanel.children [
            subPlayerView model
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    drawingSwtchBotton model dispatch
                    if model.State = Setting then
                        mediaPlayerControler model dispatch
                        pathSettings model dispatch
                ]
            ]
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
        DockPanel.margin 8
        DockPanel.children [
            headerView model dispatch
            mainPlayerView model dispatch
        ]
    ]