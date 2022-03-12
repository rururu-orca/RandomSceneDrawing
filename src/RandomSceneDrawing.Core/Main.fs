module RandomSceneDrawing.Main

open System
open Elmish
open RandomSceneDrawing.Types


module ValueTypes =
    open DrawingSettings.ValueTypes
    let frames = frames
    let duration = duration
    let interval = interval
    let playListFilePath = playListFilePath
    let snapShotFolderPath = snapShotFolderPath

    type DrawingRunning =
        { Frames: Validated<int, Frames, string>
          Duration: Validated<TimeSpan, Duration, string> }

    type DrawingInterval =
        { Interval: Validated<TimeSpan, Interval, string> }

open ValueTypes

type RandomDrawingState =
    | Setting
    | Running of DrawingRunning
    | Interval of DrawingInterval

type PlayerId =
    | MainPlayer
    | SubPlayer

type Model<'player> =
    { MainPlayer: Player.Model<'player>
      SubPlayer: Player.Model<'player>
      Settings: DrawingSettings.Model
      State: RandomDrawingState }

    member inline x.WithState s = { x with State = Running s }
    member inline x.WithState s = { x with State = Interval s }


type Msg =
    | PlayerMsg of PlayerId * Player.Msg
    | SettingsMsg of DrawingSettings.Msg
    | StartDrawing
    | StopDrawing
    | Tick


type Api = { step: unit -> Async<unit> }

type Cmds(api: Api) =
    member _.Step() =
        async {
            do! api.step ()
            return Tick
        }
        |> Cmd.OfAsync.result

let init player subPlayer =
    { MainPlayer = Player.init player
      SubPlayer = Player.init subPlayer
      Settings = DrawingSettings.init ()
      State = Setting }

let update api settingsApi playerApi msg m =
    let cmds = Cmds api
    let settingUpdate = DrawingSettings.update settingsApi
    let playerUpdate = Player.update playerApi

    let countDown map x = map ((-) (TimeSpan.FromSeconds 1.0)) x

    match msg with
    | PlayerMsg (MainPlayer, msg) ->
        let mainPlayer', cmd' = playerUpdate msg m.MainPlayer

        { m with MainPlayer = mainPlayer' }, Cmd.map ((fun m -> MainPlayer, m) >> PlayerMsg) cmd'

    | PlayerMsg (SubPlayer, msg) ->
        let player', cmd' = playerUpdate msg m.SubPlayer

        { m with SubPlayer = player' }, Cmd.map ((fun m -> SubPlayer, m) >> PlayerMsg) cmd'
    | SettingsMsg msg when m.State = Setting ->
        let settings', cmd' = settingUpdate msg m.Settings
        { m with Settings = settings' }, Cmd.map SettingsMsg cmd'
    | SettingsMsg _ -> m, Cmd.none
    | Tick ->
        match m.State with
        | Setting -> m, Cmd.none
        | Running s ->
            match countDown duration.Map s.Duration with
            | Valid _ as x -> m.WithState { s with Duration = x }, cmds.Step()
            | Invalid _ -> { m with State = Setting }, Cmd.none
        | Interval s ->
            match countDown interval.Map s.Interval with
            | Valid _ as x -> m.WithState { s with Interval = x }, cmds.Step()
            | Invalid _ -> { m with State = Setting }, Cmd.none
    | StartDrawing when m.State = Setting -> failwith "Not Implemented"
    | StartDrawing -> m, Cmd.none
    | StopDrawing when m.State = Setting -> m, Cmd.none
    | StopDrawing -> { m with State = Setting }, Cmd.none
