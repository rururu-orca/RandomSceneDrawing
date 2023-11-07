module RandomSceneDrawing.View

open System

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Shapes
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Controls.Notifications
open LibVLCSharp.Avalonia.FuncUI

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

open FSharp.Control.Reactive

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
    type Cmd<'msg> = Elmish.Cmd<'msg>

    type ElmishState<'Model, 'Msg>(writableModel: IWritable<'Model>, update) =
        member _.Model = writableModel.Current
        member _.WritableModel = writableModel
        member _.Update: 'Msg -> 'Model -> 'Model * Cmd<'Msg> = update

        member this.Dispatch(msg: 'Msg) =
            let model, cmd = this.Update msg this.Model

            for sub in cmd do
                sub this.Dispatch

            writableModel.Set model

    type IComponentContext with

        member inline this.useMap (x: IReadable<'t>) mapping =
            let x = this.usePassedRead (x, false)
            let y = (mapping >> this.useState) x.Current

            this.useEffect (
                (fun _ ->
                    let state' = mapping x.Current

                    if y.Current <> state' then
                        y.Set state'),
                [ EffectTrigger.AfterChange x ]
            )

            y, y.Current

        member inline this.useMapRead (x: IReadable<'t>) mapping =
            let y, current = this.useMap x mapping
            y :> IReadable<_>, current

        member this.useElmish<'Model, 'Msg>(init: 'Model, update) =
            let writableModel = this.useState (init, false)
            let state = ElmishState<'Model, 'Msg>(writableModel, update)

            writableModel :> IReadable<'Model>, state.Dispatch



let inline list fsCollection =
    fsCollection :> Collections.Generic.IEnumerable<'T>

let inline notFunc ([<InlineIfLambda>] f) x = not (f x)

module LibVLCSharp =
    open LibVLCSharp
    open LibVLCSharp.Shared

    let seekBar id (player: MediaPlayer) writablePosition onPositionChanged attrs =
        Component.create (
            id,
            fun ctx ->
                let minValue = 0.0
                let maxValue = 1.0

                let outlet = ctx.useState (Unchecked.defaultof<Slider>, false)
                let isPressed = ctx.useState (false, false)
                let position = ctx.usePassed writablePosition

                let handler _ : unit = position.Current |> onPositionChanged

                ctx.useEffect (handler, [ EffectTrigger.AfterChange position ])

                ctx.useEffect (
                    (fun _ ->
                        player.MediaChanged
                        |> Observable.ignore
                        |> Observable.merge (player.EndReached |> Observable.ignore)
                        |> Observable.subscribe (fun _ -> isPressed.Set false)),
                    [ EffectTrigger.AfterInit ]
                )

                ctx.useEffect (
                    (fun _ ->
                        player.EncounteredError
                        |> Observable.ignore
                        |> Observable.mergeIgnore player.Stopped
                        |> Observable.mergeIgnore player.EndReached
                        |> Observable.mergeIgnore player.SeekableChanged
                        |> Observable.mergeIgnore player.LengthChanged
                        |> Observable.mergeIgnore player.PositionChanged
                        |> Observable.map (fun _ -> float player.Position)
                        |> Observable.filter (fun p -> not isPressed.Current && minValue <= p && p <= maxValue)
                        |> Observable.subscribe (fun p -> position.Set p)),
                    [ EffectTrigger.AfterInit ]
                )

                ctx.attrs attrs

                View.createWithOutlet outlet.Set Slider.create [
                    Slider.minimum minValue
                    Slider.maximum maxValue

                    if player.IsSeekable then
                        double position.Current
                    else
                        minValue
                    |> Slider.value

                    player.IsSeekable |> Slider.isEnabled

                    Slider.onPointerPressed (fun _ -> isPressed.Set true)
                    Slider.onPointerReleased (fun _ ->
                        if isPressed.Current then
                            outlet.Current.IsEnabled <- false

                            let newPosition = outlet.Current.Value
                            let newTime = float player.Length * newPosition |> TimeSpan.FromMilliseconds


                            while not <| Threading.ThreadPool.QueueUserWorkItem(fun _ -> player.SeekTo newTime) do
                                ()


                            onPositionChanged newPosition
                            outlet.Current.IsEnabled <- true
                            isPressed.Set false)
                ]
        )



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
    let invalidTextAttrs text errors = [ TextBox.text text; List.map box errors |> TextBox.errors ]

    TextBox.create [
        match value with
        | Valid v -> domain.ToDto v |> TextBox.text
        | Invalid(CreateFailed(dto, errors)) -> yield! invalidTextAttrs dto errors
        | Invalid(UpdateFailed(ValueNone, dto, errors)) -> yield! invalidTextAttrs dto errors
        | Invalid(UpdateFailed(ValueSome before, dto, errors)) ->
            yield! invalidTextAttrs dto errors
            TextBox.onLostFocus (fun _ -> (domain.ToDto >> dispatchSetValueMsg) before)
        | Invalid(MargedError marged) -> List.map box marged |> TextBox.errors

        yield! addAttrs

        TextBox.onTextChanged dispatchSetValueMsg
    ]

let drawingSwtchBottonView model dispatch attrs =
    Component.create (
        "drawingSwtchBotton-view",
        fun ctx ->
            let _, (state, settings) =
                ctx.useMapRead model (fun m -> m.State, m.Settings.Settings)

            ctx.attrs attrs

            Button.create [

                match state with
                | Setting ->
                    Button.content "⏲ Start Drawing"
                    Button.onClick (fun _ -> StartDrawing Started |> dispatch)

                    match settings.PlayListFilePath, settings.SnapShotFolderPath with
                    | Valid _, Valid _ -> true
                    | _ -> false
                    |> Button.isEnabled
                | _ ->
                    Button.content "Stop Drawing"
                    Button.onClick (fun _ -> StopDrawing |> dispatch)
            ]
    )

let durationSecs =
    [ 10.0; 30.0; 45.0; 60.0; 90.0; 120.0; 180.0; 300.0; 600.0; 1200.0; 1800.0 ]
    |> List.map TimeSpan.FromSeconds

let durationBoxView model dispatch =
    Component.create (
        "durationBox-view",
        fun ctx ->

            let _, settings = ctx.useMapRead model (fun m -> m.Settings.Settings)

            let selected = settings.Duration |> duration.DefaultDto durationSecs[0]

            let template ts =
                TextBlock.create [ TextBlock.text $"{ts}" ]

            ComboBox.create [
                ComboBox.dataItems durationSecs
                ComboBox.selectedItem selected
                ComboBox.itemTemplate (DataTemplateView<TimeSpan>.create template)
                ComboBox.onSelectedItemChanged (function
                    | :? TimeSpan as ts -> (SetDuration >> SettingsMsg) ts |> dispatch
                    | _ -> ())
            ]
    )

let framesSettingView model dispatch =
    Component.create (
        "framesSetting-view",
        fun ctx ->

            let _, settings = ctx.useMapRead model (fun m -> m.Settings.Settings)

            NumericUpDown.create [
                NumericUpDown.minimum 1
                frames.Dto settings.Frames |> NumericUpDown.value

                NumericUpDown.onValueChanged (fun v ->
                    if v.HasValue then
                        int v.Value |> SetFrames |> SettingsMsg |> dispatch)
            ]
    )

let headerTopItemsView model dispatch =
    Component.create (
        "headerTopItems-view",
        fun ctx ->
            let _, (state, randomizeState, settings) =
                ctx.useMapRead model (fun m -> m.State, m.RandomizeState, m.Settings.Settings)

            let framesText current =
                let setting = settings.Frames

                TextBlock.create [
                    TextBlock.width 100.0
                    TextBlock.text $"%i{frames.Dto current} / {frames.Dto setting}"
                ]

            let timeText (ts: TimeSpan) =
                TextBlock.create [
                    if notFunc Deferred.inProgress randomizeState then
                        ts.ToString @"hh\:mm\:ss" |> TextBlock.text
                    else
                        TextBlock.text "Media Loading..."
                ]


            StackPanel.create [
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    drawingSwtchBottonView model dispatch []
                    match state with
                    | Setting ->
                        durationBoxView model dispatch
                        framesSettingView model dispatch
                    | Interval s ->
                        framesText s.Frames
                        interval.Dto s.Interval |> timeText
                    | Running s ->
                        framesText s.Frames
                        duration.Dto s.Duration |> timeText
                ]
            ]
    )



let subPlayerView model dispatch =
    Component.create (
        "subPlayer-view",
        fun ctx ->
            let _, (player, state, randomizeState) =
                ctx.useMapRead model (fun m -> m.SubPlayer, m.State, m.RandomizeState)

            let outlet =
                ctx.useStateLazy ((fun () -> Unchecked.defaultof<_>), renderOnChange = false)

            ctx.attrs [ Component.dock Dock.Right ]

            View.createWithOutlet outlet.Set VideoView.create [
                VideoView.height config.SubPlayer.Height
                VideoView.width config.SubPlayer.Width
                VideoView.margin (4, 4, 0, 4)
                VideoView.verticalAlignment VerticalAlignment.Top
                VideoView.horizontalAlignment HorizontalAlignment.Right
                match player with
                | Resolved player ->
                    VideoView.mediaPlayer (Some player.Player)

                    //   match state, randomizeState, player.Media with
                    //   | Interval _, _, _ -> false
                    //   | _, InProgress, _ -> false
                    //   | _, _, Resolved (Ok _) -> true
                    //   | _ -> false
                    true |> VideoView.isVideoVisible

                | _ -> ()
            ]
    )


let randomizeButtonView model dispatch attrs =
    Component.create (
        "randomizeButton-view",
        fun ctx ->
            let _, (randomizeState, settings) =
                ctx.useMapRead model (fun m -> m.RandomizeState, m.Settings.Settings)

            ctx.attrs attrs

            Button.create [
                Button.content "🔀 Show Random 🔀"
                (playListFilePath.IsValid settings.PlayListFilePath
                 && notFunc Deferred.inProgress randomizeState)
                |> Button.isEnabled
                Button.onClick (fun _ -> Randomize Started |> dispatch)
            ]
    )

let mediaPlayerControler model dispatch =

    StackPanel.create [
        StackPanel.orientation Orientation.Horizontal
        StackPanel.children [ randomizeButtonView model dispatch [] ]
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
                [
                    TextBox.column 1
                    TextBox.row 0
                    TextBox.rowSpan 2
                    TextBox.verticalAlignment VerticalAlignment.Top
                ]
                dispatchSetValueMsg
        ]
    ]

let playListFilePathView model dispatch =
    Component.create (
        "playListFilePath-View",
        fun ctx ->

            let _, value = ctx.useMapRead model (fun m -> m.Settings.Settings.PlayListFilePath)

            let buttonCallback _ =
                (PickPlayList >> SettingsMsg) Started |> dispatch

            let dispatchSetValueMsg s =
                (SetPlayListFilePath >> SettingsMsg) s |> dispatch

            ctx.attrs [ Grid.column 0 ]
            pathSelectorView playListFilePath value "PlayList" buttonCallback dispatchSetValueMsg []
    )

let snapShotFolderPathView model dispatch =
    Component.create (
        "snapShotFolderPath-View",
        fun ctx ->
            let _, value =
                ctx.useMapRead model (fun m -> m.Settings.Settings.SnapShotFolderPath)

            let buttonCallback _ =
                (PickSnapshotFolder >> SettingsMsg) Started |> dispatch

            let dispatchSetValueMsg s =
                (SetSnapShotFolderPath >> SettingsMsg) s |> dispatch

            ctx.attrs [ Grid.column 1 ]

            pathSelectorView snapShotFolderPath value "SnapShotFolder" buttonCallback dispatchSetValueMsg []
    )


let pathSettings model dispatch =
    Grid.create [
        Grid.dock Dock.Bottom
        Grid.rowDefinitions "*"
        Grid.columnDefinitions "*,*"
        Grid.children [ playListFilePathView model dispatch; snapShotFolderPathView model dispatch ]
    ]

let (|MediaResolved|_|) player =
    match player with
    | Resolved player ->
        match player.Media with
        | Resolved(Ok mediaInfo) -> Some(player, mediaInfo)
        | _ -> None
    | _ -> None


let mediaInfoView (model: IReadable<Model<LibVLCSharp.Shared.MediaPlayer>>) =
    let timeSpanText (ts: TimeSpan) = ts.ToString "hh\:mm\:ss\.ff"

    let toStackPanel children =
        StackPanel.create [ StackPanel.children [ yield! Seq.cast children ] ] :> IView

    Component.create (
        "mediaInfo-view",
        fun ctx ->
            let _, (randomizeState, main, sub) =
                ctx.useMapRead model (fun m -> m.RandomizeState, m.MainPlayer, m.SubPlayer)

            ctx.attrs[Component.dock Dock.Right]

            match randomizeState, main, sub with
            | InProgress, _, _ ->
                TextBlock.create [
                    TextBlock.horizontalAlignment HorizontalAlignment.Center
                    TextBlock.verticalAlignment VerticalAlignment.Center
                    TextBlock.text "RandomizeState InProgress..."
                ]
                :> IView
            | Resolved(Ok rs), MediaResolved(main, mainInfo), MediaResolved(sub, subInfo) ->
                seq {
                    TextBlock.create [ TextBlock.text mainInfo.Title ]

                    TextBlock.create [ rs.Position |> timeSpanText |> TextBlock.text ]

                    TextBlock.create [
                        let startTime = timeSpanText rs.StartTime
                        let endTime = timeSpanText rs.EndTime

                        TextBlock.text $"{startTime} ~ {endTime}"
                    ]
                }
                |> toStackPanel

            | _, MediaResolved(main, mainInfo), _ -> TextBlock.create [ TextBlock.text mainInfo.Title ]
            | _ -> Panel.create []
    )

let headerView model dispatch =
    Component.create (
        "header-view",
        fun ctx ->
            let _, isSetting = ctx.useMapRead model (fun m -> m.State = Setting)

            ctx.attrs[Component.dock Dock.Top]

            DockPanel.create [
                DockPanel.children [
                    subPlayerView model dispatch
                    if isSetting then
                        pathSettings model dispatch
                    StackPanel.create [
                        StackPanel.dock Dock.Left
                        StackPanel.children [ headerTopItemsView model dispatch; mediaPlayerControler model dispatch ]
                    ]
                    mediaInfoView model
                ]
            ]

    )

let seekBar id model dispatch attrs =
    Component.create (
        id,
        fun ctx ->
            let _, (deferredModel, randomizeState) =
                ctx.useMapRead model (fun m -> m.MainPlayer, m.RandomizeState)

            let hasRandomizeStateResolved = ctx.useState false

            let position = ctx.useState 0.0

            match randomizeState with
            | Resolved(Ok rs) when not hasRandomizeStateResolved.Current ->
                rs.Position / rs.MainInfo.Duration |> position.Set
                hasRandomizeStateResolved.Set true
            | HasNotStartedYet
            | InProgress when hasRandomizeStateResolved.Current ->
                position.Set 0.0
                hasRandomizeStateResolved.Set false
            | _ -> ()

            let onPositionChanged p =
                match model.Current.RandomizeState with
                | Resolved(Ok rs) -> rs.MainInfo.Duration * p |> SetRandomizeResultPosition |> dispatch
                | _ -> ()

            ctx.attrs attrs

            StackPanel.create [
                StackPanel.children [
                    match deferredModel with
                    | Resolved model -> LibVLCSharp.seekBar $"{id}-seekber" model.Player position onPositionChanged []
                    | _ -> ()
                ]
            ]
    )

let mainSeekBar model dispatch = seekBar "main" model dispatch

let mainPlayerControler id (model: IReadable<Model<LibVLCSharp.Shared.MediaPlayer>>) dispatch attrs =
    Component.create (
        id,
        fun ctx ->

            let _, (isMediaResolved, notRandomizeInPregress) =
                ctx.useMapRead model (fun m ->
                    isMediaResolved m.MainPlayer, notFunc Deferred.inProgress m.RandomizeState)

            ctx.attrs attrs

            let isSetting = model.Current.State = Setting

            StackPanel.create [
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create [
                        Button.content "Play"
                        notRandomizeInPregress |> Button.isEnabled
                        isSetting |> Button.isVisible
                        Button.onClick (fun _ -> PlayerMsg(MainPlayer, (Play Started)) |> dispatch)
                    ]
                    Button.create [
                        match model.Current.MainPlayer with
                        | Resolved { Player = player } when player.State = LibVLCSharp.Shared.VLCState.Paused ->
                            "Resume"
                        | _ -> "Pause"
                        |> Button.content
                        (isMediaResolved && notRandomizeInPregress) |> Button.isEnabled
                        Button.onClick (fun _ ->
                            PlayerMsg(MainPlayer, (Pause Started)) |> dispatch
                            ctx.forceRender ())
                    ]
                    Button.create [
                        Button.content "Stop"
                        (isMediaResolved && notRandomizeInPregress) |> Button.isEnabled
                        isSetting |> Button.isVisible
                        Button.onClick (fun _ ->
                            PlayerMsg(MainPlayer, (Stop Started)) |> dispatch
                            PlayerMsg(SubPlayer, (Stop Started)) |> dispatch)
                    ]
                ]
            ]
    )



let floatingOnSetting id model dispatch =
    let opacityMask: IBrush =
        LinearGradientBrush(
            StartPoint = RelativePoint(Point(0.0, 1.0), RelativeUnit.Relative),
            EndPoint = RelativePoint(Point(0.0, 0.0), RelativeUnit.Relative),
            GradientStops =
                (GradientStops()
                 |> tap (fun s ->
                     [
                         GradientStop(Color.Parse "Black", 0.0)
                         GradientStop(Color.Parse "Gray", 0.8)
                         GradientStop(Color.Parse "Transparent", 1.0)
                     ]
                     |> s.AddRange))
        )

    Component.create (
        id,
        fun ctx ->
            Grid.create [
                Grid.rowDefinitions "*,Auto,Auto"
                Grid.columnDefinitions "*,Auto,*"
                Grid.classes [ "floatring-content" ]
                Grid.children [
                    Rectangle.create [
                        Rectangle.row 1
                        Rectangle.rowSpan 2
                        Rectangle.column 0
                        Rectangle.columnSpan 3
                        Rectangle.fill Brushes.Black
                        Rectangle.opacity 0.5
                        Rectangle.opacityMask opacityMask
                    ]
                    mainPlayerControler "controler" model dispatch [
                        Component.row 1
                        Component.column 1
                        Component.margin 8
                    ]
                    mainSeekBar model dispatch [ Component.row 2; Component.column 0; Component.columnSpan 3 ]
                ]
            ]
    )

let floatingOnOther id model dispatch =
    Component.create (
        id,
        fun ctx ->
            Grid.create [
                Grid.rowDefinitions "*,Auto,Auto"
                Grid.columnDefinitions "*,Auto,*"
                Grid.classes [ "floatring-content" ]
                Grid.children [
                    mainSeekBar model dispatch [ Component.row 2; Component.column 0; Component.columnSpan 3 ]
                ]
            ]
    )

let mainPlayerFloating = FloatingWindow()

let mainPlayerView id model dispatch =
    Component.create (
        id,
        fun ctx ->

            let _, (player, state, randomizeState) =
                ctx.useMapRead model (fun m -> m.MainPlayer, m.State, m.RandomizeState)

            let outlet =
                ctx.useStateLazy ((fun () -> Unchecked.defaultof<VideoView>), renderOnChange = false)

            let logHander (e: LibVLCSharp.Shared.LogEventArgs) =
                match player with
                | Resolved {
                               Player = mainPlayer: LibVLCSharp.Shared.MediaPlayer
                           } ->
                    printfn "try  avcodec_send_packet critical error recover.."

                    [
                        fun () -> taskResult {
                            do! PlayerLib.LibVLCSharp.stopAsync mainPlayer
                            do! PlayerLib.LibVLCSharp.replayAsync mainPlayer
                            do! Task.delayMilliseconds 400
                            let time = mainPlayer.Time
                            mainPlayer.Time <- time
                            mainPlayer.NextFrame()
                        }
                    ]
                    |> List.map (UIThread.invokeAsync Threading.DispatcherPriority.Background)
                    |> ignore
                | _ -> ()


            ctx.useEffect (
                (fun _ ->
                    PlayerLib.LibVLCSharp.libVLC.Log
                    |> Observable.filter (fun e ->
                        e.Level = LibVLCSharp.Shared.LogLevel.Error
                        && e.Message = "avcodec_send_packet critical error")
                    |> Observable.subscribe logHander),
                [ EffectTrigger.AfterInit ]
            )

            View.createWithOutlet outlet.Set VideoView.create [
                VideoView.minHeight config.MainPlayer.Height
                VideoView.minWidth config.MainPlayer.Width
                FloatingWindowHost.floatingWindow mainPlayerFloating

                match player with
                | Resolved mainPlayer ->
                    VideoView.mediaPlayer (Some mainPlayer.Player)

                    match state, randomizeState, mainPlayer.Media with
                    | Interval _, _, _ -> false
                    | _, InProgress, _ -> false
                    | _, _, Resolved(Ok _) -> true
                    | _ -> false
                    |> VideoView.isVideoVisible

                | _ -> ()

                match state with
                | Setting -> floatingOnSetting "floatring-content-setting" model dispatch
                | _ -> floatingOnOther "floatring-content-other" model dispatch
                |> FloatingWindowHost.content
            ]
    )



let toolWindow model dispatch =
    Component.create (
        "toolWindow-view",
        fun ctx ->

            let _, state = ctx.useMapRead model (fun m -> m.State)
            let opacity = 0.3

            SubWindow.create [
                state <> Setting |> SubWindow.isVisible
                SubWindow.windowOpacity opacity
                SubWindow.content (
                    StackPanel.create [
                        StackPanel.minWidth 500
                        StackPanel.margin 4
                        StackPanel.children [
                            DockPanel.create [
                                DockPanel.lastChildFill false
                                DockPanel.children [
                                    randomizeButtonView model dispatch [ Component.dock Dock.Left ]
                                    drawingSwtchBottonView model dispatch [ Component.dock Dock.Right ]
                                ]
                            ]
                            mainPlayerControler "tool-controler" model dispatch []
                            seekBar "tool-seekber" model dispatch []
                        ]
                    ]
                )
            ]
    )

let drawingProgressView model =
    let progressValue (domain: Domain<TimeSpan, _, _>) current setting =
        let current = domain.Dto current
        let settings = domain.Dto setting

        (settings - current) / settings * 100.0 |> ProgressBar.value

    Component.create (
        "drawing-progress",
        fun ctx ->
            let _, (state, randomizeState, settings) =
                ctx.useMapRead model (fun m -> m.State, m.RandomizeState, m.Settings.Settings)

            ctx.attrs [ Component.dock Dock.Top ]

            ProgressBar.create [

                match state with
                | _ when Deferred.inProgress randomizeState ->
                    ProgressBar.foreground Brushes.LemonChiffon
                    ProgressBar.isIndeterminate true
                | Setting -> ProgressBar.value 0.0
                | Interval i ->
                    ProgressBar.foreground Brushes.LightBlue
                    progressValue interval i.Interval settings.Interval
                | Running r ->
                    ProgressBar.foreground Brushes.DodgerBlue
                    progressValue duration r.Duration settings.Duration
            ]
    )


let cmp initMainPlayer initSubPlayer init update =
    Component(
        (fun ctx ->
            let model, dispatch = ctx.useElmish (init, update)

            let _, (mainPlayer, subPlayer) =
                ctx.useMapRead model (fun m -> m.MainPlayer, m.SubPlayer)

            ctx.attrs [ Component.margin config.RootComponent.Margin ]

            ctx.useEffect (
                (fun _ ->
                    let lifetime =
                        Application.Current.ApplicationLifetime :?> IClassicDesktopStyleApplicationLifetime

                    lifetime.MainWindow.Closed |> Observable.subscribe (fun _ -> dispatch Exit)),
                [ EffectTrigger.AfterInit ]
            )

            ctx.useEffect (
                (fun _ ->
                    task {
                        let! msgs =
                            Threading.Tasks.Task.WhenAll [
                                task { return initMainPlayer () |> Finished |> InitMainPlayer }
                                task { return initSubPlayer () |> Finished |> InitSubPlayer }
                            ]

                        for msg in msgs do
                            dispatch msg
                    }
                    |> ignore),
                [ EffectTrigger.AfterInit ]
            )

            DockPanel.create [
                DockPanel.children [
                    match mainPlayer, subPlayer with
                    | Resolved _, Resolved _ ->

                        toolWindow model dispatch
                        headerView model dispatch
                        drawingProgressView model
                        mainPlayerView "main-player" model dispatch
                    | _ -> Panel.create [ Panel.children [ TextBlock.create [ TextBlock.text "Loading..." ] ] ]
                ]
            ])
    )
