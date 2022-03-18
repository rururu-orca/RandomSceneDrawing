module RandomSceneDrawing.Main

open System
open System.Threading.Tasks
open Elmish
open RandomSceneDrawing.Types
open FsToolkit
open FsToolkit.ErrorHandling


module ValueTypes =
    open DrawingSettings.ValueTypes
    let frames = frames
    let duration = duration
    let interval = interval
    let playListFilePath = playListFilePath
    let snapShotFolderPath = snapShotFolderPath

    let countDown (domain: Domain<TimeSpan, _, _>) x =
        domain.MapDto(fun ts -> ts - TimeSpan.FromSeconds 1.0) x

    type DrawingRunning =
        { Frames: Validated<int, Frames, string>
          Duration: Validated<TimeSpan, Duration, string> }

    let (|CountDownDrawing|Continue|AtLast|) (modelRunning, setting) =
        match countDown duration modelRunning.Duration with
        | Valid _ as c -> CountDownDrawing c
        | Invalid _ ->
            if frames.Dto modelRunning.Frames < frames.Dto setting then
                Continue
            else
                AtLast

    type DrawingInterval =
        { Frames: Validated<int, Frames, string>
          Interval: Validated<TimeSpan, Interval, string>
          Init: Deferred<unit> }

    let (|AtFirst|CountDownInterval|Zero|) (modelInterval) =
        if not <| Deferred.resolved modelInterval.Init then
            AtFirst(countDown interval modelInterval.Interval)
        else
            match (countDown interval modelInterval.Interval) with
            | Valid _ as c -> CountDownInterval c
            | Invalid _ -> Zero


open ValueTypes

type RandomDrawingState =
    | Setting
    | Interval of DrawingInterval
    | Running of DrawingRunning

type PlayerId =
    | MainPlayer
    | SubPlayer

type Model<'player> =
    { MainPlayer: Player.Model<'player>
      SubPlayer: Player.Model<'player>
      Settings: DrawingSettings.Model
      State: RandomDrawingState
      RandomizeState: Deferred<Result<unit, string>> }

    member inline x.WithState s = { x with State = Running s }
    member inline x.WithState s = { x with State = Interval s }

module DrawingRunning =
    let toInterval (r: DrawingRunning) (model: Model<'player>) =
        { Interval = model.Settings.Settings.Interval
          Frames = frames.MapDto((+) 1) r.Frames
          Init = HasNotStartedYet }

module DrawingInterval =
    let toRunning i (model: Model<'player>) =
        { Duration = model.Settings.Settings.Duration
          Frames = i.Frames }

module Model =
    let initInterval (model: Model<'player>) =
        model.WithState
            { Interval = model.Settings.Settings.Interval
              Frames = frames.Create 1
              Init = HasNotStartedYet }

    let setRunning (drawingInterval: DrawingInterval) (model: Model<'player>) =
        DrawingInterval.toRunning drawingInterval model
        |> model.WithState

    let setInterval (drawingRunning: DrawingRunning) (model: Model<'player>) =
        DrawingRunning.toInterval drawingRunning model
        |> model.WithState

type Msg =
    | PlayerMsg of PlayerId * Player.Msg
    | SettingsMsg of DrawingSettings.Msg
    | Randomize of AsyncOperationStatus<Result<unit, string>>
    | StartDrawing of AsyncOperationStatus<Result<unit, string>>
    | StopDrawing
    | Tick
    | Exit

type NotifyMessage =
    | InfoMsg of string
    | ErrorMsg of string

type Api<'player> =
    { step: unit -> Async<unit>
      randomize: 'player -> 'player -> Task<Result<unit, string>>
      createSnapShotFolder: string -> Task<Result<unit, string>>
      takeSnapshot: 'player -> string -> Task<Result<unit, string>>
      showInfomation: NotifyMessage -> Task<unit> }

module Api =
    let mockOk () =
        { step = fun _ -> async { do! Async.Sleep 1 }
          randomize = fun _ _ -> task { return Ok() }
          createSnapShotFolder = fun _ -> task { return Ok() }
          takeSnapshot = fun _ _ -> task { return Ok() }
          showInfomation = fun _ -> task { () } }

    let mockError () =
        { step = fun _ -> async { do! Async.Sleep 1 }
          randomize = fun _ _ -> task { return Error "Mock." }
          createSnapShotFolder = fun _ -> task { return Error "Mock." }
          takeSnapshot = fun _ _ -> task { return Error "Mock." }
          showInfomation = fun _ -> task { () } }


open System.IO

type Cmds<'player>(api: Api<'player>, mainPlayer, subPlayer) =
    let getMediaTitle (media: Deferred<Result<MediaInfo, string>>) =
        match media with
        | Resolved (Ok info) -> Ok info.Title
        | Resolved (Error e) -> Error e
        | _ -> Error "Not Resolved."

    member _.Step() =
        async {
            do! api.step ()
            return Tick
        }
        |> Cmd.OfAsync.result

    member _.Randomize() =
        task { return! api.randomize mainPlayer subPlayer }

    member this.RandomizeCmd() =
        task {
            let! result = this.Randomize()
            return (Finished >> Randomize) result
        }
        |> Cmd.OfTask.result

    member this.StartDrawingCmd path =
        taskResult {
            let! path' = resultOr snapShotFolderPath path
            do! api.createSnapShotFolder path'
        }
        |> Task.map (Finished >> StartDrawing)
        |> Cmd.OfTask.result

    member _.TakeSnapshot path currentFrames media =
        taskResult {
            let! path' = resultOr snapShotFolderPath path
            and! frames = resultOr frames currentFrames
            and! title = getMediaTitle media

            let path =
                Path.Combine [|
                    path'
                    $"%03i{frames} {title}.png"
                |]

            return! api.takeSnapshot mainPlayer path
        }
        |> ignore

    member _.ShowInfomation info =
        task { do! api.showInfomation info } |> ignore


let init player subPlayer onExitHandler =
    { MainPlayer = Player.init player
      SubPlayer = Player.init subPlayer
      Settings = DrawingSettings.init ()
      State = Setting
      RandomizeState = HasNotStartedYet },

    fun dispatch ->
        onExitHandler
        |> Observable.add (fun e -> dispatch Exit)
    |> Cmd.ofSub

let update api settingsApi playerApi msg m =
    let cmds = Cmds(api, m.MainPlayer.Player, m.SubPlayer.Player)
    let settingUpdate = DrawingSettings.update settingsApi
    let playerUpdate = Player.update playerApi

    let settings = m.Settings.Settings

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
    | Exit ->
        let settings', cmd' = settingUpdate DrawingSettings.Msg.SaveSettings m.Settings
        { m with Settings = settings' }, Cmd.map SettingsMsg cmd'
    | Randomize Started when m.RandomizeState = InProgress -> m, Cmd.none
    | Randomize Started -> { m with RandomizeState = InProgress }, cmds.RandomizeCmd()
    | Randomize (Finished result) ->
        match result, m.State with
        | Ok (), Interval i when i.Init = InProgress ->
            { m with RandomizeState = Resolved result }
                .WithState { i with Init = Resolved() },
            Cmd.none
        | _ -> { m with RandomizeState = Resolved result }, Cmd.none
    | Tick ->
        let updateRamdomize (model: Model<'player>) =
            { model with RandomizeState = InProgress },
            Cmd.batch [
                cmds.RandomizeCmd()
                cmds.Step()
            ]

        match m.State with
        | Setting -> m, Cmd.none
        | _ when m.RandomizeState = InProgress -> m, cmds.Step()
        | _ when m.RandomizeState |> Deferred.exists Result.isError -> updateRamdomize m
        | Interval s ->
            match s with
            | AtFirst x ->
                m.WithState { s with Init = InProgress }
                |> updateRamdomize
            | CountDownInterval x -> m.WithState { s with Interval = x }, cmds.Step()
            | Zero -> Model.setRunning s m, cmds.Step()
        | Running s ->
            match s, settings.Frames with
            | CountDownDrawing x -> m.WithState { s with Duration = x }, cmds.Step()
            | Continue ->
                cmds.TakeSnapshot settings.SnapShotFolderPath s.Frames m.MainPlayer.Media
                Model.setInterval s m, cmds.Step()

            | AtLast ->
                cmds.TakeSnapshot settings.SnapShotFolderPath s.Frames m.MainPlayer.Media

                { m with State = Setting }, Cmd.none
    | StartDrawing Started when m.State = Setting ->
        Model.initInterval m, cmds.StartDrawingCmd settings.SnapShotFolderPath
    | StartDrawing Started -> m, Cmd.none
    | StartDrawing (Finished (Error error)) ->
        cmds.ShowInfomation(ErrorMsg error)

        { m with State = Setting }, Cmd.none
    | StartDrawing (Finished (Ok _)) -> m, cmds.Step()
    | StopDrawing when m.State = Setting -> m, Cmd.none
    | StopDrawing -> { m with State = Setting }, Cmd.none
