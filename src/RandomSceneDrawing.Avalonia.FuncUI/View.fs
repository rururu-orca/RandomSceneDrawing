module RandomSceneDrawing.View

open System

open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Controls.Notifications
open LibVLCSharp.Avalonia.FuncUI

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types

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

                    if y.Current <> state' then y.Set state'),
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

let drawingSwtchBottonView model dispatch =
    Component.create (
        "drawingSwtchBotton-view",
        fun ctx ->
            let _, (state, settings) =
                ctx.useMapRead model (fun m -> m.State, m.Settings.Settings)

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

let durationBoxView model dispatch =
    Component.create (
        "durationBox-view",
        fun ctx ->

            let _, settings = ctx.useMapRead model (fun m -> m.Settings.Settings)

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
    )

let framesSettingView model dispatch =
    Component.create (
        "framesSetting-view",
        fun ctx ->

            let _, settings = ctx.useMapRead model (fun m -> m.Settings.Settings)

            NumericUpDown.create [
                NumericUpDown.minimum 1.0
                frames.Dto settings.Frames |> NumericUpDown.value
                NumericUpDown.onValueChanged (int >> SetFrames >> SettingsMsg >> dispatch)
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
                    drawingSwtchBottonView model dispatch
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

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open FSharp.Control.Reactive

let subPlayerView model dispatch =
    Component.create (
        "subPlayer-view",
        fun ctx ->
            let _, (player, state, randomizeState) =
                ctx.useMapRead model (fun m -> m.SubPlayer, m.State, m.RandomizeState)

            let outlet = ctx.useState (VideoView(), renderOnChange = false)

            ctx.useEffect ((fun _ -> FloatingContent.showAtMe outlet.Current), [ EffectTrigger.AfterInit ])

            ctx.attrs [ Component.dock Dock.Right ]

            View.createWithOutlet
                outlet.Set
                VideoView.create
                [ VideoView.height config.SubPlayer.Height
                  VideoView.width config.SubPlayer.Width
                  VideoView.margin (4, 4, 0, 4)
                  VideoView.verticalAlignment VerticalAlignment.Top
                  VideoView.horizontalAlignment HorizontalAlignment.Right
                  match player with
                  | Resolved player ->
                      VideoView.mediaPlayer player.Player

                      (Deferred.resolved player.Media
                       && isNotInterval state
                       && notFunc Deferred.inProgress randomizeState)
                      |> VideoView.isVideoVisible

                    //   VideoView.content (
                    //       Button.create [
                    //           Button.content "Full Screen"
                    //           Button.onClick (fun e ->
                    //               let lifetime =
                    //                   Application.Current.ApplicationLifetime
                    //                   :?> IClassicDesktopStyleApplicationLifetime

                    //               lifetime.MainWindow.WindowState <- WindowState.FullScreen)
                    //       ]
                    //   )
                  | _ -> () ]
    )


let randomizeButtonView model dispatch =
    Component.create (
        "randomizeButton-view",
        fun ctx ->
            let _, (randomizeState, settings) =
                ctx.useMapRead model (fun m -> m.RandomizeState, m.Settings.Settings)

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
        StackPanel.children [
            randomizeButtonView model dispatch
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
                (PickSnapshotFolder >> SettingsMsg) Started
                |> dispatch

            let dispatchSetValueMsg s =
                (SetSnapShotFolderPath >> SettingsMsg) s
                |> dispatch

            ctx.attrs [ Grid.column 1 ]

            pathSelectorView snapShotFolderPath value "SnapShotFolder" buttonCallback dispatchSetValueMsg []
    )


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

let (|MediaResolved|_|) player =
    match player with
    | Resolved player ->
        match player.Media with
        | Resolved (Ok mediaInfo) -> Some(player, mediaInfo)
        | _ -> None
    | _ -> None


let mediaInfoView (model: IReadable<Model<LibVLCSharp.MediaPlayer>>) =
    let timeSpanText (ts: TimeSpan) = ts.ToString "hh\:mm\:ss\.ff"

    let toStackPanel children =
        StackPanel.create [
            StackPanel.children [
                yield! Seq.cast children
            ]
        ]
        :> IView

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
            | Resolved (Ok rs), MediaResolved (main, mainInfo), MediaResolved (sub, subInfo) ->
                seq {
                    TextBlock.create [
                        TextBlock.text mainInfo.Title
                    ]

                    TextBlock.create [
                        match main.Player.Time with
                        | 0L -> rs.Position |> timeSpanText
                        | time ->
                            PlayerLib.Utils.toSecf time
                            |> TimeSpan.FromSeconds
                            |> timeSpanText
                        |> TextBlock.text
                    ]

                    TextBlock.create [
                        let startTime = timeSpanText rs.StartTime
                        let endTime = timeSpanText rs.EndTime

                        TextBlock.text $"{startTime} ~ {endTime}"
                    ]
                }
                |> toStackPanel

            | _, MediaResolved (main, mainInfo), _ ->
                TextBlock.create [
                    TextBlock.text mainInfo.Title
                ]
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
                        StackPanel.children [
                            headerTopItemsView model dispatch
                            mediaPlayerControler model dispatch
                        ]
                    ]
                    mediaInfoView model
                ]
            ]

    )


let mainPlayerControler id model dispatch =
    Component.create (
        id,
        fun ctx ->

            let _, (isMediaResolved, notRandomizeInPregress) =
                ctx.useMapRead model (fun m ->
                    isMediaResolved m.MainPlayer, notFunc Deferred.inProgress m.RandomizeState)

            StackPanel.create [
                StackPanel.dock Dock.Bottom
                StackPanel.horizontalAlignment HorizontalAlignment.Center
                StackPanel.verticalAlignment VerticalAlignment.Bottom
                StackPanel.orientation Orientation.Horizontal
                StackPanel.children [
                    Button.create [
                        Button.content "Play"
                        notRandomizeInPregress |> Button.isEnabled
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

let floatingOnSetting id model dispatch =
    Component.create (
        id,
        fun ctx ->
            DockPanel.create [
                DockPanel.classes [
                    "floatring-content"
                ]
                DockPanel.children [
                    mainPlayerControler "controler" model dispatch
                ]
            ]
    )

let floatingOnOther id model dispatch =
    Component.create (
        id,
        fun ctx ->
            let model = ctx.usePassedRead (model, renderOnChange = false)

            DockPanel.create [
                DockPanel.classes [
                    "floatring-content"
                ]
                DockPanel.children [
                    Rectangle.create [
                        Rectangle.fill Brushes.DarkBlue
                        Rectangle.width 200
                        Rectangle.height 50
                    ]
                ]
            ]
    )

let mainPlayerFloatingName = "mainplayer-floating"
let mainPlayerFloating = FloatingWindow mainPlayerFloatingName

let mainPlayerView id model dispatch =
    Component.create (
        id,
        fun ctx ->

            let mgr = WindowNotificationManager(mainPlayerFloating, Position = NotificationPosition.BottomRight, MaxItems = 3)


            let _, (player, state, randomizeState) =
                ctx.useMapRead model (fun m -> m.MainPlayer, m.State, m.RandomizeState)

            let outlet = ctx.useState (VideoView(), renderOnChange = false)

            ctx.useEffect ((fun _ -> 
                let name = outlet.Current.FloatingWindow.FloatingWindowName
                FloatingContent.showAtMe outlet.Current), [ EffectTrigger.AfterInit ])

            View.createWithOutlet
                outlet.Set
                VideoView.create
                [ VideoView.minHeight config.MainPlayer.Height
                  VideoView.minWidth config.MainPlayer.Width
                  FloatingOwner.floatingWindow mainPlayerFloating

                  match player with
                  | Resolved mainPlayer ->
                      VideoView.mediaPlayer mainPlayer.Player

                      Notification("Info", "Test", NotificationType.Information)
                      |> mgr.Show


                      match state, randomizeState, mainPlayer.Media with
                      | Interval _, _, _ -> false
                      | _, InProgress, _ -> false
                      | _, _, Resolved (Ok _) -> true
                      | _ -> false
                      |> VideoView.isVideoVisible

                  | _ -> ()

                  match state with
                  | Setting -> floatingOnSetting "floatring-content-setting" model dispatch
                  | _ -> floatingOnOther "floatring-content-other" model dispatch
                  |> VideoView.content ]
    )

let toolWindow model dispatch =
    Component.create (
        "toolWindow-view",
        fun ctx ->

            let _, state = ctx.useMapRead model (fun m -> m.State)

            SubWindow.create [
                state <> Setting |> SubWindow.isVisible
                SubWindow.content (
                    Panel.create [
                        Panel.margin 16
                        Panel.children [
                            randomizeButtonView model dispatch
                        ]
                    ]
                )
            ]
    )

let drawingProgressView model =
    let progressValue (domain: Domain<TimeSpan, _, _>) current setting =
        let current = domain.Dto current
        let settings = domain.Dto setting

        (settings - current) / settings * 100.0
        |> ProgressBar.value

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

open Avalonia
open Avalonia.Controls.ApplicationLifetimes

let cmp initMainPlayer initSubPlayer init update =
    Component(
        (fun ctx ->
            let model, dispatch = ctx.useElmish (init, update)

            let _, (mainPlayer, subPlayer) =
                ctx.useMapRead model (fun m -> m.MainPlayer, m.SubPlayer)

            ctx.attrs [
                Component.margin config.RootComponent.Margin
            ]
            ctx.useEffect (
                (fun _ ->


                    let lifetime =
                        Application.Current.ApplicationLifetime :?> IClassicDesktopStyleApplicationLifetime

                    lifetime.MainWindow.Closed
                    |> Observable.subscribe (fun _ -> dispatch Exit)),
                [ EffectTrigger.AfterInit ]
            )

            ctx.useEffect (
                (fun _ ->
                    task {
                        let! msg = backgroundTask { return initMainPlayer () |> Finished |> InitMainPlayer }
                        dispatch msg
                    }
                    |> ignore

                    task {
                        let! msg = backgroundTask { return initSubPlayer () |> Finished |> InitSubPlayer }
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
                    | _ ->
                        Panel.create [
                            Panel.children [
                                TextBlock.create [
                                    TextBlock.text "Loading..."
                                ]
                            ]
                        ]
                ]
            ])
    )
