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


[<AutoOpen>]
module CustomHooks =
    open Avalonia.Threading
    type Cmd<'msg> = Elmish.Cmd<'msg>

    type ElmishState<'Model, 'Msg>(writableModel: IWritable<'Model>, update) =
        member this.Model = writableModel.Current
        member this.WritableModel = writableModel
        member this.Update: 'Msg -> 'Model -> 'Model * Cmd<'Msg> = update

        member this.Dispatch(msg: 'Msg) =
            let model, cmd = this.Update msg this.Model

            for sub in cmd do
                sub this.Dispatch

            let set () = writableModel.Set model

            if Dispatcher.UIThread.CheckAccess() then
                set ()
            else
                Dispatcher.UIThread.Post set


    type IComponentContext with

        member this.useElmish<'Model, 'Msg>(init: 'Model * Cmd<'Msg>, update) =
            let initModel, initCmd = init
            let writableModel = this.useState (initModel, true)
            let state = ElmishState<'Model, 'Msg>(writableModel, update)

            this.useEffect (
                handler =
                    (fun _ ->
                        for initSub in initCmd do
                            initSub state.Dispatch),
                triggers = [ EffectTrigger.AfterInit ]
            )

            state


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

let mainPlayerControler id mainPlayer randomizeInPregress dispatch =
    Component.create (
        id,
        fun ctx ->
            let mainPlayer = ctx.usePassedRead mainPlayer
            let randomizeInPregress = ctx.usePassedRead randomizeInPregress

            let notRandomizeInPregress = not randomizeInPregress.Current
            let isMediaResolved = isMediaResolved mainPlayer.Current

            StackPanel.create [
                StackPanel.dock Dock.Bottom
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create [
                        Button.content "Play"
                        (notRandomizeInPregress) |> Button.isEnabled
                        Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Play Started)) |> dispatch)
                    ]
                    Button.create [
                        Button.content "Pause"
                        (isMediaResolved && notRandomizeInPregress)
                        |> Button.isEnabled
                        Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Pause Started)) |> dispatch)
                    ]
                    Button.create [
                        Button.content "Stop"
                        (isMediaResolved && notRandomizeInPregress)
                        |> Button.isEnabled
                        Button.onClick (fun _ ->
                            PlayerMsg(MainPlayer, (Stop Started)) |> dispatch
                            PlayerMsg(SubPlayer, (Stop Started)) |> dispatch)
                    ]
                ]
            ]
    )

let floatingOnSetting id mainPlayer randomizeInPregress dispatch =
    Component.create (
        id,
        fun ctx ->
            DockPanel.create [
                DockPanel.classes [
                    "floatring-content"
                ]
                DockPanel.children [
                    mainPlayerControler "controler" mainPlayer randomizeInPregress dispatch
                ]
            ]
    )

let floatingOnOther id mainPlayer dispatch =
    Component.create (
        id,
        fun ctx ->
            let mainPlayer = ctx.usePassedRead mainPlayer

            DockPanel.create [
                DockPanel.classes [
                    "floatring-content"
                ]
                DockPanel.children []
            ]
    )

let mainPlayerView id model mainPlayer dispatch =
    Component.create (
        id,
        fun ctx ->
            let model = ctx.usePassedRead model
            let mainPlayer = ctx.usePassedRead mainPlayer
            let randomizeInPregress = ctx.useState false

            ctx.useEffect (
                handler =
                    (fun _ ->
                        match model.Current.RandomizeState with
                        | InProgress ->
                            if not randomizeInPregress.Current then
                                randomizeInPregress.Set true
                        | _ ->
                            if randomizeInPregress.Current then
                                randomizeInPregress.Set false),
                triggers = [ EffectTrigger.AfterChange model ]
            )

            VideoView.create [

                match mainPlayer.Current with
                | Resolved mainPlayer ->
                    VideoView.mediaPlayer mainPlayer.Player

                    match mainPlayer.Media with
                    | Resolved (Ok _) when not randomizeInPregress.Current -> true
                    | _ -> false
                    |> VideoView.isVideoVisible
                | _ -> ()

                VideoView.hasFloating true
                match model.Current.State with
                | Setting -> floatingOnSetting "floatring-content-setting" mainPlayer randomizeInPregress dispatch
                | _ -> floatingOnOther "floatring-content-other" mainPlayer dispatch
                |> VideoView.content
            ]
    )

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
    let progressValue (domain: Domain<TimeSpan, _, _>) current setting =
        let current = domain.Dto current
        let settings = domain.Dto setting

        (settings - current) / settings * 100.0
        |> ProgressBar.value

    let settings = model.Settings.Settings

    ProgressBar.create [
        ProgressBar.dock Dock.Top
        ProgressBar.classes [
            "drawingProgress"
        ]
        match model.State with
        | _ when Deferred.inProgress model.RandomizeState ->
            ProgressBar.foreground "LemonChiffon"
            ProgressBar.isIndeterminate true
        | Setting -> ProgressBar.value 0.0
        | Interval i ->
            ProgressBar.foreground "LightBlue"
            progressValue interval i.Interval settings.Interval
        | Running r ->
            ProgressBar.foreground "DodgerBlue"
            progressValue duration r.Duration settings.Duration
    ]

let cmp init update =
    Component(
        (fun ctx ->
            let state = ctx.useElmish (init, update)
            let model = state.Model
            let dispatch = state.Dispatch

            let mainPlayer = ctx.useState model.MainPlayer
            let isInterval = ctx.useState false

            ctx.useEffect (
                handler =
                    (fun _ ->
                        let currentRoot = state.WritableModel.Current

                        if mainPlayer.Current <> currentRoot.MainPlayer then
                            mainPlayer.Set currentRoot.MainPlayer

                        match currentRoot.State with
                        | Interval _ when not isInterval.Current -> isInterval.Set true
                        | Setting when isInterval.Current -> isInterval.Set false
                        | Running _ when isInterval.Current -> isInterval.Set false
                        | _ -> ()),
                triggers = [ EffectTrigger.AfterChange state.WritableModel ]
            )

            DockPanel.create [
                DockPanel.margin 8
                DockPanel.children [
                    toolWindow model dispatch
                    headerView model dispatch
                    drawingProgressView model
                    mainPlayerView "main-player" state.WritableModel mainPlayer dispatch
                ]
            ])
    )
