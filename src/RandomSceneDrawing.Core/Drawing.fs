module RandomSceneDrawing.Drawing

open System
open System.Threading.Tasks
open System.IO
open Util
open Types
open Types.ErrorTypes
open Types.Validator

module ValueTypes =
    // Settings
    type Frames = private Frames of int
    let (|Frames|) (Frames i) = i
    let frames = Validator(Frames, (fun (Frames i) -> i), validateIfPositiveNumber)

    type Duration = private Duration of TimeSpan
    let (|Duration|) (Duration d) = d
    let duration = Validator(Duration, (fun (Duration d) -> d), validateIfPositiveTime)

    type Interval = private Interval of TimeSpan
    let (|Interval|) (Interval i) = i
    let interval = Validator(Interval, (fun (Interval d) -> d), validateIfPositiveTime)

    type PlayListFilePath = private PlayListFilePath of string
    let (|PlayListFilePath|) (PlayListFilePath p) = p

    let playListFilePath =
        Validator(PlayListFilePath, (fun (PlayListFilePath p) -> p), validatePathString File)

    type SnapShotFolderPath = private SnapShotFolderPath of string
    let (|SnapShotFolderPath|) (SnapShotFolderPath p) = p

    let snapShotFolderPath =
        Validator(SnapShotFolderPath, (fun (SnapShotFolderPath p) -> p), validatePathString Directory)

open ValueTypes

type Settings =
    { Frames: Validated<int, Frames, string>
      Duration: Validated<TimeSpan, Duration, string>
      Interval: Validated<TimeSpan, Interval, string>
      PlayListFilePath: Validated<string, PlayListFilePath, string>
      SnapShotFolderPath: Validated<string, SnapShotFolderPath, string> }

    static member Default() =
        { Frames = frames.Create config.Frames
          Duration = duration.Create config.Duration
          Interval = interval.Create config.Interval
          PlayListFilePath = playListFilePath.Create config.PlayListFilePath
          SnapShotFolderPath = snapShotFolderPath.Create config.SnapShotFolderPath }

type DrawingStopped =
    { Settings: Settings
      PickedPlayListPath: Deferred<Result<string, FilePickerError>>
      PickedSnapShotFolderPath: Deferred<Result<string, FilePickerError>> }

    member inline x.WithSettings([<InlineIfLambda>] f) = { x with Settings = f x.Settings }

module DrawingStopped =
    let create settings =
        { Settings = settings
          PickedPlayListPath = HasNotStartedYet
          PickedSnapShotFolderPath = HasNotStartedYet }

    let inline withSettings (x: DrawingStopped) ([<InlineIfLambda>] f) = x.WithSettings f

type DrawingRunning =
    { CurrentDuration: Validated<TimeSpan, Duration, string>
      CurrentFrames: Validated<int, Frames, string>
      Settings: Settings }

    member inline x.WithSettings([<InlineIfLambda>] f) = { x with Settings = f x.Settings }

module DrawingRunning =
    let ofStopped (x: DrawingStopped) =
        { CurrentDuration = x.Settings.Duration
          CurrentFrames = x.Settings.Frames
          Settings = x.Settings }

    let inline withSettings (x: DrawingRunning) ([<InlineIfLambda>] f) = x.WithSettings f


type Model =
    | Stopped of DrawingStopped
    | Running of DrawingRunning

type Msg =
    | Tick
    | SetFrames of int
    | SetDuration of TimeSpan
    | SetInterval of TimeSpan
    | SetPlayListFilePath of string
    | PickPlayList of AsyncOperationStatus<Result<string, FilePickerError>>
    | SetSnapShotFolderPath of string
    | PickSnapshotFolder of AsyncOperationStatus<Result<string, FilePickerError>>

type Api =
    { step: unit -> Async<unit>
      pickPlayList: unit -> Task<string>
      pickSnapshotFolder: unit -> Task<string> }

let init () =
    { Settings = Settings.Default()
      PickedPlayListPath = HasNotStartedYet
      PickedSnapShotFolderPath = HasNotStartedYet }
    |> Stopped

open Elmish
open Elmish.Cmd.OfAsync

let update api msg m =
    match m with
    | Stopped m ->
        match msg with
        | Tick -> Stopped m, Cmd.none
        | SetFrames x ->
            (m.WithSettings >> Stopped) (fun m -> { m with Frames = m.Frames |> frames.Update x }), Cmd.none
        | SetDuration x ->
            (m.WithSettings >> Stopped) (fun m -> { m with Duration = m.Duration |> duration.Update x }), Cmd.none
        | SetInterval x ->
            (m.WithSettings >> Stopped) (fun m -> { m with Interval = m.Interval |> interval.Update x }), Cmd.none
        | SetPlayListFilePath x ->
            (m.WithSettings >> Stopped) (fun m ->
                { m with PlayListFilePath = m.PlayListFilePath |> playListFilePath.Update x }),
            Cmd.none
        | PickPlayList (_) -> failwith "Not Implemented"
        | SetSnapShotFolderPath x ->
            (m.WithSettings >> Stopped) (fun m ->
                { m with
                    SnapShotFolderPath =
                        m.SnapShotFolderPath
                        |> snapShotFolderPath.Update x }),
            Cmd.none
        | PickSnapshotFolder (_) -> failwith "Not Implemented"
    | Running m ->
        match msg with
        | Tick ->
            let nextDuration =
                m.CurrentDuration
                |> duration.Map((-) (TimeSpan.FromSeconds 1.0))

            match nextDuration with
            | Valid _ -> Running { m with CurrentDuration = nextDuration }, perform api.step () (fun _ -> Tick)
            | Invalid _ -> (DrawingStopped.create >> Stopped) m.Settings, Cmd.none

        | SetFrames (_) -> failwith "Not Implemented"
        | SetDuration (_) -> failwith "Not Implemented"
        | SetInterval (_) -> failwith "Not Implemented"
        | SetPlayListFilePath (_) -> failwith "Not Implemented"
        | SetSnapShotFolderPath (_) -> failwith "Not Implemented"
        | PickPlayList (_) -> failwith "Not Implemented"
        | PickSnapshotFolder (_) -> failwith "Not Implemented"
