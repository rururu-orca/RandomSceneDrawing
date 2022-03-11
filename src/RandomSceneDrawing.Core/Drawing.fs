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

type DrawingInfo =
    { CurrentDuration: Validated<TimeSpan, Duration, string>
      CurrentFrames: Validated<int, Frames, string>
      Settings: Settings }

type Model =
    | Stopped of Settings
    | Running of DrawingInfo

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

let init () = Settings.Default() |> Stopped

open Elmish
open Elmish.Cmd.OfAsync

let update api msg m =
    match m with
    | Stopped m ->
        match msg with
        | Tick -> Stopped m, Cmd.none
        | SetFrames x -> Stopped { m with Frames = m.Frames |> frames.Update x }, Cmd.none
        | SetDuration x -> Stopped { m with Duration = m.Duration |> duration.Update x }, Cmd.none
        | SetInterval x -> Stopped { m with Interval = m.Interval |> interval.Update x }, Cmd.none
        | SetPlayListFilePath x ->
            Stopped { m with PlayListFilePath = m.PlayListFilePath |> playListFilePath.Update x }, Cmd.none
        | PickPlayList (_) -> failwith "Not Implemented"
        | SetSnapShotFolderPath x ->
            Stopped
                { m with
                    SnapShotFolderPath =
                        m.SnapShotFolderPath
                        |> snapShotFolderPath.Update x },
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
            | Invalid _ -> (Stopped m.Settings), Cmd.none

        | SetFrames (_) -> failwith "Not Implemented"
        | SetDuration (_) -> failwith "Not Implemented"
        | SetInterval (_) -> failwith "Not Implemented"
        | SetPlayListFilePath (_) -> failwith "Not Implemented"
        | SetSnapShotFolderPath (_) -> failwith "Not Implemented"
        | PickPlayList (_) -> failwith "Not Implemented"
        | PickSnapshotFolder (_) -> failwith "Not Implemented"
