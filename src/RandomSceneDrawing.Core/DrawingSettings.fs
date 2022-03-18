module RandomSceneDrawing.DrawingSettings

open System
open System.Threading.Tasks
open System.IO
open Util
open Types
open Types.ErrorTypes
open Types.Validator

module ValueTypes =
    type Frames = private Frames of int
    let (|Frames|) (Frames i) = i
    let frames = Domain(Frames, (fun (Frames i) -> i), validateIfPositiveNumber)

    type Duration = private Duration of TimeSpan
    let (|Duration|) (Duration d) = d
    let duration = Domain(Duration, (fun (Duration d) -> d), validateIfPositiveTime)

    type Interval = private Interval of TimeSpan
    let (|Interval|) (Interval i) = i
    let interval = Domain(Interval, (fun (Interval d) -> d), validateIfPositiveTime)

    type PlayListFilePath = private PlayListFilePath of string
    let (|PlayListFilePath|) (PlayListFilePath p) = p

    let playListFilePath =
        Domain(PlayListFilePath, (fun (PlayListFilePath p) -> p), validatePathString File)

    type SnapShotFolderPath = private SnapShotFolderPath of string
    let (|SnapShotFolderPath|) (SnapShotFolderPath p) = p

    let snapShotFolderPath =
        Domain(SnapShotFolderPath, (fun (SnapShotFolderPath p) -> p), validatePathString Directory)

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

module Settings =
    let save settings =
        config.Frames <- frames.Dto settings.Frames
        config.Duration <- duration.Dto settings.Duration
        config.Interval <- interval.Dto settings.Interval
        config.PlayListFilePath <- playListFilePath.Dto settings.PlayListFilePath
        config.SnapShotFolderPath <- snapShotFolderPath.Dto settings.SnapShotFolderPath
        config.Save changedConfigPath

type Model =
    { Settings: Settings
      PickedPlayListPath: Deferred<Result<string, FilePickerError>>
      PickedSnapShotFolderPath: Deferred<Result<string, FilePickerError>> }

    member inline x.WithSettings([<InlineIfLambda>] f) = { x with Settings = f x.Settings }

module Model =
    let create settings =
        { Settings = settings
          PickedPlayListPath = HasNotStartedYet
          PickedSnapShotFolderPath = HasNotStartedYet }

    let inline withSettings (x: Model) ([<InlineIfLambda>] f) = x.WithSettings f

type Msg =
    | SetFrames of int
    | SetDuration of TimeSpan
    | SetInterval of TimeSpan
    | SetPlayListFilePath of string
    | PickPlayList of AsyncOperationStatus<Result<string, FilePickerError>>
    | SetSnapShotFolderPath of string
    | PickSnapshotFolder of AsyncOperationStatus<Result<string, FilePickerError>>
    | SaveSettings

type Api =
    { pickPlayList: unit -> Task<Result<string, FilePickerError>>
      pickSnapshotFolder: unit -> Task<Result<string, FilePickerError>> }

open Elmish

type Cmds(api: Api) =

    member _.PickPlayList() =
        task {
            let! result = api.pickPlayList ()
            return (Finished >> PickPlayList) result
        }
        |> Cmd.OfTask.result

    member _.PickSnapshotFolder() =
        task {
            let! result = api.pickSnapshotFolder ()
            return (Finished >> PickSnapshotFolder) result
        }
        |> Cmd.OfTask.result


let init () = Settings.Default() |> Model.create

let update api msg (m: Model) =
    let cmds = Cmds api

    match msg with
    | SetFrames x -> (m.WithSettings) (fun m -> { m with Frames = m.Frames |> frames.Update x }), Cmd.none
    | SetDuration x -> m.WithSettings(fun m -> { m with Duration = m.Duration |> duration.Update x }), Cmd.none
    | SetInterval x -> m.WithSettings(fun m -> { m with Interval = m.Interval |> interval.Update x }), Cmd.none
    | SetPlayListFilePath x ->
        m.WithSettings(fun m -> { m with PlayListFilePath = m.PlayListFilePath |> playListFilePath.Update x }), Cmd.none
    | PickPlayList Started when m.PickedPlayListPath = InProgress -> m, Cmd.none
    | PickPlayList Started -> { m with PickedPlayListPath = InProgress }, cmds.PickPlayList()
    | PickPlayList (Finished (Ok x as result)) ->
        let m' =
            fun (m: Settings) -> { m with PlayListFilePath = m.PlayListFilePath |> playListFilePath.Update x }
            |> Model.withSettings { m with PickedPlayListPath = Resolved result }

        m', Cmd.none
    | PickPlayList (Finished (Error _ as result)) -> { m with PickedPlayListPath = Resolved result }, Cmd.none
    | SetSnapShotFolderPath x ->
        m.WithSettings (fun m ->
            { m with
                SnapShotFolderPath =
                    m.SnapShotFolderPath
                    |> snapShotFolderPath.Update x }),
        Cmd.none
    | PickSnapshotFolder Started when m.PickedSnapShotFolderPath = InProgress -> m, Cmd.none
    | PickSnapshotFolder Started -> { m with PickedSnapShotFolderPath = InProgress }, cmds.PickSnapshotFolder()
    | PickSnapshotFolder (Finished (Ok x as result)) ->
        let m' =
            fun (m: Settings) ->
                { m with
                    SnapShotFolderPath =
                        m.SnapShotFolderPath
                        |> snapShotFolderPath.Update x }
            |> Model.withSettings { m with PickedSnapShotFolderPath = Resolved result }

        m', Cmd.none
    | PickSnapshotFolder (Finished (Error _ as result)) ->
        { m with PickedSnapShotFolderPath = Resolved result }, Cmd.none
    | SaveSettings ->
        Settings.save m.Settings
        m, Cmd.none
