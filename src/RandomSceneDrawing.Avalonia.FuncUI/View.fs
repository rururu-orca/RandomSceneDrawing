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

let durationSecs =
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
    |> List.map TimeSpan.FromSeconds

let durationBox model dispatch =
    let settings = model.Settings.Settings

    let selected =
        settings.Duration
        |> duration.DefaultDto durationSecs[0]

    let template ts =
        TextBlock.create [
            TextBlock.text $"{ts}"
        ]

    StackPanel.create [
        StackPanel.children [
            ComboBox.create [
                ComboBox.dataItems durationSecs
                ComboBox.selectedItem selected
                ComboBox.itemTemplate (DataTemplateView<TimeSpan>.create template)
                ComboBox.onSelectedItemChanged (function
                    | :? TimeSpan as ts -> (SetDuration >> SettingsMsg) ts |> dispatch
                    | _ -> ())
            ]
        ]
    ]

let framesSettingView model dispatch =
    let settings = model.Settings.Settings

    NumericUpDown.create [
        NumericUpDown.minimum 1.0
        frames.Dto settings.Frames |> NumericUpDown.value
        NumericUpDown.onValueChanged (int >> SetFrames >> SettingsMsg >> dispatch)
    ]

let headerTopItems model dispatch =
    let framesText f =
        TextBlock.create [
            TextBlock.width 100.0
            TextBlock.text $"%i{frames.Dto f}"
        ]

    let timeText (ts: TimeSpan) =
        TextBlock.create [
            if notFunc Deferred.inProgress model.RandomizeState then
                ts.ToString @"hh\:mm\:ss" |> TextBlock.text
            else
                TextBlock.text "Media Loading..."
        ]

    StackPanel.create [
        StackPanel.children [
            drawingSwtchBotton model dispatch
            match model.State with
            | Setting ->
                durationBox model dispatch
                framesSettingView model dispatch
            | Interval s ->
                framesText s.Frames
                interval.Dto s.Interval |> timeText
            | Running s ->
                framesText s.Frames
                duration.Dto s.Duration |> timeText
        ]
    ]

let subPlayerView model =
    VideoView.create [
        VideoView.height config.SubPlayer.Height
        VideoView.width config.SubPlayer.Width
        VideoView.margin (4, 4, 0, 4)
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
                (isMediaResolved model.MainPlayer
                 && notFunc Deferred.inProgress model.RandomizeState)
                |> Button.isEnabled
                Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Pause Started)) |> dispatch)
            ]
            Button.create [
                Button.content "Stop"
                (isMediaResolved model.MainPlayer
                 && notFunc Deferred.inProgress model.RandomizeState)
                |> Button.isEnabled
                Button.onClick (fun _ ->
                    PlayerMsg(MainPlayer, (Stop Started)) |> dispatch
                    PlayerMsg(SubPlayer, (Stop Started)) |> dispatch)
            ]
        ]
    ]

let pathSelectorView domain value (buttonText: string) buttonCallback dispatchSetValueMsg addAttrs =

    Grid.create [
        Grid.rowDefinitions "Auto,*"
        Grid.columnDefinitions "Auto,*"
        Grid.margin (0, 0, 4, 0)
        yield! addAttrs
        Grid.children [
            Button.create [
                Button.column 0
                Button.row 0
                Button.content buttonText
                Button.onClick buttonCallback
            ]
            validatedTextBox
                domain
                value
                [ TextBox.column 1
                  TextBox.row 0
                  TextBox.rowSpan 2
                  TextBox.verticalAlignment VerticalAlignment.Top ]
                dispatchSetValueMsg
        ]
    ]

let playListFilePathView model dispatch =
    let value = model.Settings.Settings.PlayListFilePath

    let buttonCallback _ =
        (PickPlayList >> SettingsMsg) Started |> dispatch

    let dispatchSetValueMsg s =
        (SetPlayListFilePath >> SettingsMsg) s |> dispatch

    pathSelectorView playListFilePath value "PlayList" buttonCallback dispatchSetValueMsg [ Grid.column 0 ]


let snapShotFolderPathView model dispatch =
    let value = model.Settings.Settings.SnapShotFolderPath

    let buttonCallback _ =
        (PickSnapshotFolder >> SettingsMsg) Started
        |> dispatch

    let dispatchSetValueMsg s =
        (SetSnapShotFolderPath >> SettingsMsg) s
        |> dispatch

    pathSelectorView snapShotFolderPath value "SnapShotFolder" buttonCallback dispatchSetValueMsg [ Grid.column 1 ]

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
        DockPanel.margin (4, 0, 0, 0)
        DockPanel.dock Dock.Top
        DockPanel.children [
            subPlayerView model
            StackPanel.create [
                StackPanel.orientation Orientation.Vertical
                StackPanel.children [
                    headerTopItems model dispatch
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
