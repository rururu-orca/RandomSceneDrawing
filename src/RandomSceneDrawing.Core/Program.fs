module RandomSceneDrawing.Program

open System
open System.IO
open FSharp.Configuration
open Types
open RandomSceneDrawing
open FSharpPlus

type Config = YamlConfig<"Config.yaml">

let changedConfigPath =
    Path.Combine [|
        AppDomain.CurrentDomain.BaseDirectory
        "ChangedConfig.yaml"
    |]

let config = Config()

let init () =
    if File.Exists changedConfigPath then
        config.Load changedConfigPath

    { Frames = config.Frames
      Duration = config.Duration
      Interval = config.Interval
      Player = PlayerLib.initPlayer()
      SubPlayer = PlayerLib.initPlayer()
      PlayerState = PlayerState.Stopped
      MediaDuration = TimeSpan.Zero
      MediaPosition = TimeSpan.Zero
      PlayerBufferCache = 0.0f
      RandomizeState = Waiting
      PlayListFilePath = config.PlayListFilePath
      SnapShotFolderPath = config.SnapShotFolderPath
      SnapShotPath = ""
      Title = ""
      RandomDrawingState = RandomDrawingState.Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0
      StatusMessage = "" },
    []

let requireGreaterThan1Frame input =
    [ if input.Frames < 1 then
          $"Frames must greater than 1" ]

let requireDurationGreaterThan input =
    let ts = TimeSpan(0, 0, 1)

    [ if input.Duration < ts then
          $"Frames must greater than {ts}" ]

let mapCanExec =
    function
    | [] -> true
    | _ -> false

let getSnapShotPath model =
    Path.Combine [|
        model.SnapShotPath
        $"%03i{model.CurrentFrames}.png"
    |]

let validateDuration =
    function
    | t when t < TimeSpan(0, 0, 10) -> Error BelowLowerLimit
    | t when TimeSpan(99, 99, 99) < t -> Error OverUpperLimit
    | t -> Ok t

let setDuration (m: Model) newValue =
    newValue
    |> validateDuration
    |> Result.map (fun t -> { m with Duration = t })
    |> Result.defaultValue m

let validateFrames =
    function
    | t when t < 1 -> Error BelowLowerLimit
    | t when 999 < t -> Error OverUpperLimit
    | t -> Ok t

let setFrames (m: Model) newValue =
    newValue
    |> validateFrames
    |> Result.map (fun t -> { m with Frames = t })
    |> Result.defaultValue m

let update msg m =
    match msg with
    // Player
    | RequestPlay -> m, [ Play m.Player ]
    | PlayCandeled -> m, []
    | PlaySuccess mediaInfo ->
        { m with
              Title = mediaInfo.Title
              PlayerState = Playing
              MediaDuration = mediaInfo.Duration },
        []
    | PlayFailed ex ->
        match ex with
        | PlayFailedException (str) -> m, [ ShowErrorInfomation str ]
        | _ -> m, [ ShowErrorInfomation ex.Message ]
    | RequestPause -> m, [ Pause m.Player ]
    | PauseSuccess state -> { m with PlayerState = state }, []
    | PauseFailed ex -> m, [ ShowErrorInfomation ex.Message ]
    | RequestStop -> m, [ Stop m.Player ]
    | StopSuccess -> { m with PlayerState = Stopped }, []
    | StopFailed ex -> m, [ ShowErrorInfomation ex.Message ]
    | PlayerTimeChanged time -> { m with MediaPosition = time }, []
    | PlayerBuffering cache ->
        match cache with
        | 100.0f when m.RandomizeState = WaitBuffering ->
            { m with
                  PlayerBufferCache = cache
                  RandomizeState = Waiting },
            []
        | _ -> { m with PlayerBufferCache = cache }, []

    // Random Drawing Setting
    | SetFrames x -> setFrames m x, []
    | IncrementFrames n -> setFrames m (m.Frames + n), []
    | DecrementFrames n -> setFrames m (m.Frames - n), []
    | SetDuration x -> setDuration m x, []
    | IncrementDuration time -> setDuration m (m.Duration + time), []
    | DecrementDuration time -> setDuration m (m.Duration - time), []
    | SetPlayListFilePath path -> { m with PlayListFilePath = path }, []
    | RequestSelectPlayListFilePath -> m, [ SelectPlayListFilePath ]
    | SelectPlayListFilePathSuccess path -> { m with PlayListFilePath = path }, []
    | SelectPlayListFilePathCanceled _ -> m, []
    | SelectPlayListFilePathFailed ex -> m, [ ShowErrorInfomation ex.Message ]

    | SetSnapShotFolderPath path -> { m with SnapShotFolderPath = path }, []
    | RequestSelectSnapShotFolderPath -> m, [ SelectSnapShotFolderPath ]
    | SelectSnapShotFolderPathSuccess path -> { m with SnapShotFolderPath = path }, []
    | SelectSnapShotFolderPathCandeled -> m, []
    | SelectSnapShotFolderPathFailed ex -> m, [ ShowErrorInfomation ex.Message ]

    // Random Drawing
    | RequestRandomize (_) ->
        { m with RandomizeState = Running }, [ Randomize(m.Player, m.SubPlayer, m.PlayListFilePath) ]
    | RandomizeSuccess (_) ->
        { m with
              Title = m.Player.Media.Meta LibVLCSharp.Shared.MetadataType.Title
              PlayerState = Playing
              RandomizeState =
                  if m.PlayerBufferCache = 100.0f then
                      Waiting
                  else
                      WaitBuffering
              MediaPosition = (float m.Player.Time |> TimeSpan.FromMilliseconds)
              MediaDuration = (float m.Player.Length |> TimeSpan.FromMilliseconds) },
        [ Pause m.Player ]
    | RandomizeFailed ex ->
        match ex with
        | :? TimeoutException ->
            { m with PlayerState = Stopped },
            [ Stop m.Player
              Randomize(m.Player, m.SubPlayer, m.PlayListFilePath) ]
        | PlayFailedException (str) ->
            { m with RandomizeState = Waiting },
            [ ShowErrorInfomation str
              Stop m.Player ]
        | _ ->
            { m with RandomizeState = Waiting },
            [ ShowErrorInfomation ex.Message
              Stop m.Player ]
    | RequestStartDrawing (_) ->
        m,
        [ CreateCurrentSnapShotFolder m.SnapShotFolderPath
          StartDrawing ]
    | RequestStopDrawing (_) -> m, [ StopDrawing ]
    | StartDrawingSuccess (_) ->
        { m with
              CurrentFrames = 1
              CurrentDuration = m.Interval
              RandomDrawingState = Interval
              RandomizeState = Running },
        [ Randomize(m.Player, m.SubPlayer, m.PlayListFilePath) ]
    | CreateCurrentSnapShotFolderSuccess path -> { m with SnapShotPath = path }, []
    | StartDrawingFailed ex -> m, [ ShowErrorInfomation ex.Message ]
    | StopDrawingSuccess ->
        { m with
              RandomDrawingState = RandomDrawingState.Stop },
        []
    | Tick ->
        let nextDuration = m.CurrentDuration - TimeSpan(0, 0, 1)

        let (|RunningCountDown|IntervalFinished|CurrentFrameFinished|RandomDrawingFinished|) m =
            if nextDuration > TimeSpan.Zero then
                RunningCountDown
            elif m.RandomDrawingState = Interval then
                IntervalFinished
            elif m.CurrentFrames < m.Frames then
                CurrentFrameFinished
            else
                RandomDrawingFinished

        match m with
        | RunningCountDown ->
            { m with
                  CurrentDuration = nextDuration },
            []
        | IntervalFinished ->
            { m with
                  RandomDrawingState = RandomDrawingState.Running
                  CurrentDuration = m.Duration },
            []
        | CurrentFrameFinished ->
            { m with
                  RandomDrawingState = Interval
                  RandomizeState = Running
                  CurrentFrames = m.CurrentFrames + 1
                  CurrentDuration = m.Interval },
            [ (m.Player, getSnapShotPath m) |> TakeSnapshot
              Randomize(m.Player, m.SubPlayer, m.PlayListFilePath) ]
        | RandomDrawingFinished ->
            { m with
                  CurrentDuration = TimeSpan.Zero },
            [ (m.Player, getSnapShotPath m) |> TakeSnapshot
              StopDrawing ]
        |> (function
        | PlayerLib.AlreadyBufferingCompleted (m', msg') -> { m' with RandomizeState = Waiting }, msg'
        | next -> next)
    | TakeSnapshotSuccess -> m, []
    | TakeSnapshotFailed ex ->
        match ex with
        | SnapShotFailedException (str) -> m, [ ShowErrorInfomation str ]
        | _ -> m, [ ShowErrorInfomation ex.Message ]
    // Util
    | LayoutUpdated p -> { m with StatusMessage = p }, []
    | WindowClosed ->
        config.Duration <- m.Duration
        config.Frames <- m.Frames
        config.Interval <- m.Interval
        config.PlayListFilePath <- m.PlayListFilePath
        config.SnapShotFolderPath <- m.SnapShotFolderPath
        config.Save changedConfigPath
        m, []
    | ResetSettings ->
        let origin = Config()

        { m with
              Duration = origin.Duration
              Frames = origin.Frames
              Interval = origin.Interval
              PlayListFilePath = origin.PlayListFilePath
              SnapShotFolderPath = origin.SnapShotFolderPath },
        []
    | ShowErrorInfomationSuccess -> m, []
