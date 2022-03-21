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

    ComboBox.create [
        ComboBox.dataItems durationSecs
        ComboBox.selectedItem selected
        ComboBox.itemTemplate (DataTemplateView<TimeSpan>.create template)
        ComboBox.onSelectedItemChanged (function
            | :? TimeSpan as ts -> (SetDuration >> SettingsMsg) ts |> dispatch
            | _ -> ())
    ]


let framesSettingView model dispatch =
    let settings = model.Settings.Settings

    NumericUpDown.create [
        NumericUpDown.minimum 1.0
        frames.Dto settings.Frames |> NumericUpDown.value
        NumericUpDown.onValueChanged (int >> SetFrames >> SettingsMsg >> dispatch)
    ]

let headerTopItems model dispatch =
    let framesText current =
        let setting = model.Settings.Settings.Frames
        TextBlock.create [
            TextBlock.width 100.0
            TextBlock.text $"%i{frames.Dto current} / {frames.Dto setting}"
        ]

    let timeText (ts: TimeSpan) =
        TextBlock.create [
            if notFunc Deferred.inProgress model.RandomizeState then
                ts.ToString @"hh\:mm\:ss" |> TextBlock.text
            else
                TextBlock.text "Media Loading..."
        ]

    StackPanel.create [
        StackPanel.orientation Orientation.Horizontal
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
        if Deferred.inProgress model.RandomizeState then
            VideoView.content (ProgressBar.create [])
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
        StackPanel.orientation Orientation.Horizontal
        StackPanel.children [
            randomizeButton model dispatch
            if model.State = Setting then
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
        Grid.rowDefinitions "Auto,28"
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
        Grid.dock Dock.Bottom
        Grid.rowDefinitions "*"
        Grid.columnDefinitions "*,*"
        Grid.children [
            playListFilePathView model dispatch
            snapShotFolderPathView model dispatch
        ]
    ]

let (|RandomizeResolved|PlayResolved|NotYet|) model =
    match model.RandomizeState, model.MainPlayer, model.SubPlayer with
    | Resolved (Ok rs), Resolved main, Resolved sub ->
        match main.Media, sub.Media with
        | Resolved (Ok mainInfo), Resolved (Ok subInfo) -> RandomizeResolved(rs, main, mainInfo, sub, subInfo)
        | _ -> NotYet
    | _, Resolved main, _ when model.RandomizeState <> InProgress ->
        match main.Media with
        | Resolved (Ok mainInfo) -> PlayResolved(main, mainInfo)
        | _ -> NotYet
    | _ -> NotYet

let mediaInfoView (model: Model<LibVLCSharp.MediaPlayer>) =
    let timeSpanText (ts: TimeSpan) = ts.ToString "hh\:mm\:ss\.ff"

    if model.RandomizeState = InProgress then
        TextBlock.create [
            TextBlock.horizontalAlignment HorizontalAlignment.Stretch
            TextBlock.verticalAlignment VerticalAlignment.Center
            TextBlock.text "RandomizeState InProgress..."
        ]
        :> IView
    else
        StackPanel.create [
            StackPanel.dock Dock.Right
            StackPanel.children [
                match model with
                | RandomizeResolved (rs, main, mainInfo, sub, subInfo) ->
                    TextBlock.create [
                        TextBlock.text mainInfo.Title
                    ]

                    TextBlock.create [
                        match main.Player.Time with
                        | 0L -> rs.Position |> timeSpanText
                        | time ->
                            PlayerLib.Helper.toSecf time
                            |> TimeSpan.FromSeconds
                            |> timeSpanText
                        |> TextBlock.text
                    ]

                    TextBlock.create [
                        let startTime = timeSpanText rs.StartTime
                        let endTime = timeSpanText rs.EndTime

                        TextBlock.text $"{startTime} ~ {endTime}"
                    ]
                | PlayResolved (main, mainInfo) ->
                    TextBlock.create [
                        TextBlock.text mainInfo.Title
                    ]
                | NotYet -> ()
            ]
        ]

let headerView model dispatch =
    DockPanel.create [
        DockPanel.margin (4, 0, 0, 0)
        DockPanel.dock Dock.Top
        DockPanel.children [
            subPlayerView model
            if model.State = Setting then
                pathSettings model dispatch
            StackPanel.create [
                StackPanel.dock Dock.Left
                StackPanel.children [
                    headerTopItems model dispatch
                    mediaPlayerControler model dispatch
                ]
            ]
            mediaInfoView model
        ]
    ]

let floatingContent model dispatch =
    Panel.create [
        Panel.children [
            Rectangle.create [
                Rectangle.classes [ "videoViewBlind" ]
                match model with
                | NotYet -> true
                | _ -> false
                |> Rectangle.isVisible
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

let toolWindow model dispatch =
    SubWindow.create [
        model.State <> Setting |> SubWindow.isVisible
        SubWindow.content (
            Panel.create [
                Panel.margin 16
                Panel.children [
                    randomizeButton model dispatch
                ]
            ]
        )
    ]

let drawingProgressView model =
    ProgressBar.create [
        ProgressBar.dock Dock.Top
        ProgressBar.classes ["drawingProgress"]
        match model.State with
        | Setting -> 0.0
        | Interval i ->
            let current = interval.Dto i.Interval
            let settings = interval.Dto model.Settings.Settings.Interval
            (settings - current) / settings * 100.0
        | Running r ->
            let current = duration.Dto r.Duration
            let settings = duration.Dto model.Settings.Settings.Duration
            (settings - current) / settings * 100.0
        |> ProgressBar.value
    ]

let view model dispatch =
    DockPanel.create [
        DockPanel.margin 8
        DockPanel.children [
            toolWindow model dispatch
            headerView model dispatch
            drawingProgressView model
            mainPlayerView model dispatch
        ]
    ]
