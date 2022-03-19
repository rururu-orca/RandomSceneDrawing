module RandomSceneDrawing.Main

open System
open System.Threading.Tasks
open Elmish
open RandomSceneDrawing.Types
open FsToolkit.ErrorHandling


module ValueTypes =
    open DrawingSettings.ValueTypes
    open Validator

    let frames = frames
    let duration = duration
    let interval = interval
    let playListFilePath = playListFilePath
    let snapShotFolderPath = snapShotFolderPath

    type SnapShotPath = private SnapShotPath of string

    let snapShotPath =
        Domain(SnapShotPath, (fun (SnapShotPath p) -> p), validatePathString Directory)

    let (|SnapShotPath|) (SnapShotPath sp) = sp

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

    type RandomizeSource = PlayList of PlayListFilePath

open ValueTypes

let (|PlayListFilePath|) rs =
    match rs with
    | PlayList (DrawingSettings.ValueTypes.PlayListFilePath pl) -> pl

type RandomDrawingState =
    | Setting
    | Interval of DrawingInterval
    | Running of DrawingRunning

type PlayerId =
    | MainPlayer
    | SubPlayer

type Model<'player> =
    { MainPlayer: Deferred<Player.Model<'player>>
      SubPlayer: Deferred<Player.Model<'player>>
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

type Msg<'player> =
    | InitMainPlayer of AsyncOperationStatus<'player>
    | InitSubPlayer of AsyncOperationStatus<'player>
    | PlayerMsg of PlayerId * Player.Msg
    | SettingsMsg of DrawingSettings.Msg
    | Randomize of AsyncOperationStatus<Result<unit, string>>
    | StartDrawing of AsyncOperationStatus<Result<unit, string>>
    | StopDrawing
    | Tick
    | Exit

type Api<'player> =
    { step: unit -> Async<unit>
      randomize: RandomizeSource -> 'player -> 'player -> Task<Result<unit, string>>
      createSnapShotFolder: string -> Task<Result<string, string>>
      takeSnapshot: 'player -> string -> Task<Result<unit, string>>
      copySubVideo: string -> Task<Result<unit, string>>
      showInfomation: NotifyMessage -> Task<unit> }

module Api =
    let mockOk () =
        { step = fun _ -> async { do! Async.Sleep 1 }
          randomize = fun _ _ _ -> task { return Ok() }
          createSnapShotFolder = fun _ -> task { return Ok "test" }
          takeSnapshot = fun _ _ -> task { return Ok() }
          copySubVideo = fun _ -> task { return Ok() }
          showInfomation = fun _ -> task { () } }

    let mockError () =
        { step = fun _ -> async { do! Async.Sleep 1 }
          randomize = fun _ _ _ -> task { return Error "Mock." }
          createSnapShotFolder = fun _ -> task { return Error "Mock." }
          takeSnapshot = fun _ _ -> task { return Error "Mock." }
          copySubVideo = fun _ -> task { return Error "Mock." }
          showInfomation = fun _ -> task { () } }


open System.IO

type Cmds<'player>
    (
        api: Api<'player>,
        mainPlayer: Deferred<Player.Model<'player>>,
        subPlayer: Deferred<Player.Model<'player>>
    )
     =

    let mutable snapShotFolder = Error "Has not set yet."

    let getMediaTitle (media: Deferred<Result<MediaInfo, string>>) =
        match media with
        | Resolved (Ok info) -> Ok info.Title
        | Resolved (Error e) -> Error e
        | _ -> Error "Not Resolved."

    let askMainPlayer () =
        match mainPlayer with
        | Resolved mp -> Ok mp.Player
        | _ -> Error "Main player has not loading."

    let askSubPlayer () =
        match subPlayer with
        | Resolved mp -> Ok mp.Player
        | _ -> Error "Sub player has not loading."

    let tryToRandomizeSource domain pl =
        resultDomainOr domain pl |> Result.map PlayList

    let showInfomation info =
        task { do! api.showInfomation info } |> ignore

    member _.ShowInfomation info = showInfomation info

    member _.Step() =
        async {
            do! api.step ()
            return Tick
        }
        |> Cmd.OfAsync.result

    member _.Randomize domain randomizeSource =
        taskResult {
            let! mainPlayer = askMainPlayer ()
            and! subPlayer = askSubPlayer ()
            and! rs = tryToRandomizeSource domain randomizeSource

            return! api.randomize rs mainPlayer subPlayer
        }
        |> TaskResult.teeError (ErrorMsg >> showInfomation)

    member this.RandomizeCmd domain randomizeSource =
        task {
            let! result = this.Randomize domain randomizeSource
            return (Finished >> Randomize) result
        }
        |> Cmd.OfTask.result

    member this.StartDrawingCmd path =
        taskResult {
            let! path' = resultDtoOr snapShotFolderPath path
            let! path'' = api.createSnapShotFolder path'

            snapShotFolder <-
                snapShotPath.Create path''
                |> resultDomainOr snapShotPath
        }
        |> TaskResult.teeError (ErrorMsg >> showInfomation)
        |> Task.map (Finished >> StartDrawing)
        |> Cmd.OfTask.result

    member _.TakeSnapshot currentFrames media =
        taskResult {
            let! mainPlayer = askMainPlayer ()
            and! (SnapShotPath path') = snapShotFolder
            and! frames = resultDtoOr frames currentFrames
            and! title = getMediaTitle media

            let path =
                Path.Combine [|
                    path'
                    $"%03i{frames} {title}.png"
                |]

            return! api.takeSnapshot mainPlayer path
        }
        |> TaskResult.teeError (ErrorMsg >> showInfomation)
        |> ignore

    member _.CopySubVideo currentFrames media =
        taskResult {
            let! (SnapShotPath path') = snapShotFolder
            and! frames = resultDtoOr frames currentFrames
            and! title = getMediaTitle media

            let path =
                Path.Combine [|
                    path'
                    $"%03i{frames} {title}.mp4"
                |]

            do! api.copySubVideo path
        }
        |> TaskResult.teeError (ErrorMsg >> showInfomation)
        |> ignore

let init player subPlayer onExitHandler =
    let initMainPlayer =
        task { return (player >> Finished >> InitMainPlayer) () }
        |> Cmd.OfTask.result

    let initSubPlayer =
        task { return (subPlayer >> Finished >> InitSubPlayer) () }
        |> Cmd.OfTask.result

    let onExit =
        fun dispatch ->
            onExitHandler
            |> Observable.add (fun e -> dispatch Exit)
        |> Cmd.ofSub

    { MainPlayer = HasNotStartedYet
      SubPlayer = HasNotStartedYet
      Settings = DrawingSettings.init ()
      State = Setting
      RandomizeState = HasNotStartedYet },
    Cmd.batch [
        initMainPlayer
        initSubPlayer
        onExit
    ]


let update api settingsApi playerApi msg m =
    let settingUpdate = DrawingSettings.update settingsApi
    let playerUpdate = Player.update playerApi

    let settings = m.Settings.Settings
    let cmds = Cmds(api, m.MainPlayer, m.SubPlayer)

    match msg with
    | InitMainPlayer (Finished player) -> { m with MainPlayer = (Player.init >> Resolved) player }, Cmd.none
    | InitMainPlayer _ -> m, Cmd.none
    | InitSubPlayer (Finished player) -> { m with SubPlayer = (Player.init >> Resolved) player }, Cmd.none
    | InitSubPlayer _ -> m, Cmd.none
    | PlayerMsg (MainPlayer, msg) ->
        match m.MainPlayer with
        | Resolved mainPlayer ->
            let mainPlayer', cmd' = playerUpdate msg mainPlayer

            { m with MainPlayer = Resolved mainPlayer' }, Cmd.map ((fun m -> MainPlayer, m) >> PlayerMsg) cmd'
        | _ -> m, Cmd.none

    | PlayerMsg (SubPlayer, msg) ->
        match m.SubPlayer with
        | Resolved subPlayer ->
            let player', cmd' = playerUpdate msg subPlayer

            { m with SubPlayer = Resolved player' }, Cmd.map ((fun m -> SubPlayer, m) >> PlayerMsg) cmd'
        | _ -> m, Cmd.none

    | SettingsMsg msg when m.State = Setting ->
        let settings', cmd' = settingUpdate msg m.Settings
        { m with Settings = settings' }, Cmd.map SettingsMsg cmd'
    | SettingsMsg _ -> m, Cmd.none
    | Exit ->
        let settings', cmd' = settingUpdate DrawingSettings.Msg.SaveSettings m.Settings
        { m with Settings = settings' }, Cmd.map SettingsMsg cmd'
    | Randomize Started when m.RandomizeState = InProgress -> m, Cmd.none
    | Randomize Started ->
        { m with RandomizeState = InProgress }, cmds.RandomizeCmd playListFilePath settings.PlayListFilePath
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
                cmds.RandomizeCmd playListFilePath settings.PlayListFilePath
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
            match (s, settings.Frames), m.MainPlayer, m.SubPlayer with
            | CountDownDrawing x, Resolved _, Resolved _ -> m.WithState { s with Duration = x }, cmds.Step()
            | Continue, Resolved mainPlayer, Resolved subPlayer ->
                cmds.TakeSnapshot s.Frames mainPlayer.Media
                cmds.CopySubVideo s.Frames subPlayer.Media
                Model.setInterval s m, cmds.Step()

            | AtLast, Resolved mainPlayer, Resolved subPlayer ->
                cmds.TakeSnapshot s.Frames mainPlayer.Media
                cmds.CopySubVideo s.Frames subPlayer.Media

                { m with State = Setting }, Cmd.none
            | _ -> m, cmds.Step()
    | StartDrawing Started when m.State = Setting ->
        Model.initInterval m, cmds.StartDrawingCmd settings.SnapShotFolderPath
    | StartDrawing Started -> m, Cmd.none
    | StartDrawing (Finished (Error error)) ->
        cmds.ShowInfomation(ErrorMsg error)

        { m with State = Setting }, Cmd.none
    | StartDrawing (Finished (Ok _)) ->
        InfoMsg "Drawing Started." |> cmds.ShowInfomation
        m, cmds.Step()
    | StopDrawing when m.State = Setting -> m, Cmd.none
    | StopDrawing ->
        InfoMsg "Drawing Stoped." |> cmds.ShowInfomation
        { m with State = Setting }, Cmd.none
