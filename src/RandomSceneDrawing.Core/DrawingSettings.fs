module RandomSceneDrawing.DrawingSettings

open System
open System.Threading.Tasks
open System.IO
open Util
open FSharp.Configuration
open FsToolkit.ErrorHandling
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

    type MediaInfo = { Title: string; Duration: TimeSpan }


    type TrimDuration = { Start: TimeSpan; End: TimeSpan }

    type RandomizeInfoListDtoYaml = YamlConfig<"RandomizeInfoSample.yaml">


    let randomizeInfoListDtoPath =
        Path.Combine [|
            AppDomain.CurrentDomain.BaseDirectory
            "RandomizeInfoListDto.yaml"
        |]

    let randomizeInfoListDto = RandomizeInfoListDtoYaml()

    if File.Exists randomizeInfoListDtoPath then
        randomizeInfoListDto.Load randomizeInfoListDtoPath


    type MediaInfoDtoYaml = RandomizeInfoListDtoYaml.RandomizeInfoDto_Item_Type.MediaInfo_Type
    type RandomizeInfoDtoYaml = RandomizeInfoListDtoYaml.RandomizeInfoDto_Item_Type


    module MediaInfo =
        let ofYamlConfig (dto: MediaInfoDtoYaml) =
            { Title = dto.Title
              Duration = dto.Duration }

        let toYamlConfig (info) =
            MediaInfoDtoYaml(Title = info.Title, Duration = info.Duration)

    type TrimDurationDtoYaml = RandomizeInfoDtoYaml.TrimDurations_Item_Type

    module TrimDuration =
        let validate value =
            result {
                let! _ = validateIfPositiveTime value.Start
                and! _ = validateIfPositiveTime value.End

                and! _ =
                    value.Start < value.End
                    |> Result.requireTrue [
                        "Must be Start < End."
                       ]

                return value
            }

        let ofYamlConfig (dto: TrimDurationDtoYaml) = { Start = dto.Start; End = dto.End }

        let toYamlConfig dur =
            TrimDurationDtoYaml(Start = dur.Start, End = dur.End)


    type RandomizeInfoDto =
        { Id: Guid
          MediaInfo: MediaInfo
          Path: string
          TrimDurations: TrimDuration list }

    module RandomizeInfoDto =
        let validateTrimDurations dto =
            result {
                match dto.TrimDurations with
                | [ t ] ->
                    let! _ = TrimDuration.validate t

                    if t.End <= dto.MediaInfo.Duration then
                        return! Ok()
                    else
                        return!
                            Error [
                                "MediaInfo.Duration < TrimDuration.End"
                            ]
                | ts ->
                    for (ts1, ts2) in List.pairwise ts do
                        let! _ = TrimDuration.validate ts1

                        if ts1.End > ts2.Start then
                            return! Error [ $"{ts1} < {ts2}" ]

                    if ts[-1].End <= dto.MediaInfo.Duration then
                        return! Ok()
                    else
                        return!
                            Error [
                                "MediaInfo.Duration < TrimDuration.End"
                            ]

            }

        let ofYamlConfig (yaml: RandomizeInfoDtoYaml) =
            { Id = Guid.NewGuid()
              TrimDurations =
                yaml.TrimDurations
                |> Seq.map TrimDuration.ofYamlConfig
                |> Seq.toList
              MediaInfo = MediaInfo.ofYamlConfig yaml.MediaInfo
              Path = yaml.Path }

        let toYamlConfig dto =
            let yaml = RandomizeInfoDtoYaml()
            yaml.Path <- dto.Path
            yaml.MediaInfo.Duration <- dto.MediaInfo.Duration
            yaml.MediaInfo.Title <- dto.MediaInfo.Title

            yaml.TrimDurations <-
                dto.TrimDurations
                |> List.map TrimDuration.toYamlConfig
                |> Collections.Generic.List

            yaml

        let mock = ofYamlConfig RandomizeInfoListDtoYaml().RandomizeInfoDto[0]

    type RandomizeInfo =
        private
            { Id: Guid
              MediaInfo: MediaInfo
              Path: string
              TrimDurations: TrimDuration list }

    type ValidatedRandomizeInfo = Validated<RandomizeInfoDto, RandomizeInfo, string>

    let initRandomizeInfoDomain validator =
        Domain<RandomizeInfoDto, RandomizeInfo, string>(
            (fun dto ->
                { Id = dto.Id
                  MediaInfo = dto.MediaInfo
                  Path = dto.Path
                  TrimDurations = dto.TrimDurations }),
            (fun domain ->
                { Id = domain.Id
                  MediaInfo = domain.MediaInfo
                  Path = domain.Path
                  TrimDurations = domain.TrimDurations }),
            (fun dto -> result { return! validator dto })
        )


    module RandomizeInfo =
        let mock =
            let randomizeInfo = initRandomizeInfoDomain (fun info -> Ok info)
            randomizeInfo.Create RandomizeInfoDto.mock


        let tryGetMediaInfo
            (randomizeInfo: Domain<RandomizeInfoDto, RandomizeInfo, _>)
            (info: Validated<RandomizeInfoDto, _, _>)
            =
            match info with
            | Valid v -> Ok (randomizeInfo.ToDto v).MediaInfo
            | Invalid (CreateFailed (dto, _)) -> Ok dto.MediaInfo
            | Invalid (UpdateFailed (_, dto, _)) -> Ok dto.MediaInfo
            | _ -> Error "MediaInfo get Failed..."

open ValueTypes



type Settings =
    { Frames: Validated<int, Frames, string>
      Duration: Validated<TimeSpan, Duration, string>
      Interval: Validated<TimeSpan, Interval, string>
      RandomizeInfoList: list<ValidatedRandomizeInfo>
      PlayListFilePath: Validated<string, PlayListFilePath, string>
      SnapShotFolderPath: Validated<string, SnapShotFolderPath, string> }

    static member Default(randomizeInfo: Domain<RandomizeInfoDto, RandomizeInfo, string>) =
        { Frames = frames.Create config.Frames
          Duration = duration.Create config.Duration
          Interval = interval.Create config.Interval
          RandomizeInfoList =
            List.ofSeq randomizeInfoListDto.RandomizeInfoDto
            |> List.map (
                RandomizeInfoDto.ofYamlConfig
                >> randomizeInfo.Create
            )

          PlayListFilePath = playListFilePath.Create config.PlayListFilePath
          SnapShotFolderPath = snapShotFolderPath.Create config.SnapShotFolderPath }

module Settings =
    let dtoOrEmptyString (domain: Domain<string, _, _>) value =
        domain.DtoOr
            (function
            | UpdateFailed ((ValueSome c), _, _) -> (domain.ofDomain >> domain.Dto) c
            | _ -> "")
            value

    let save (randomizeInfo: Domain<RandomizeInfoDto, RandomizeInfo, string>) settings =
        config.Frames <- frames.Dto settings.Frames
        config.Duration <- duration.Dto settings.Duration
        config.Interval <- interval.Dto settings.Interval
        config.PlayListFilePath <- dtoOrEmptyString playListFilePath settings.PlayListFilePath
        config.SnapShotFolderPath <- dtoOrEmptyString snapShotFolderPath settings.SnapShotFolderPath
        config.Save changedConfigPath

        randomizeInfoListDto.RandomizeInfoDto <-
            settings.RandomizeInfoList
            |> List.choose (function
                | Valid v -> (randomizeInfo.ToDto >> Some) v
                | Invalid (CreateFailed (dto, _)) -> Some dto
                | Invalid (UpdateFailed ((ValueSome v), _, _)) -> (randomizeInfo.ToDto >> Some) v
                | Invalid (UpdateFailed ((ValueNone), dto, _)) -> Some dto
                | _ -> None)
            |> List.map RandomizeInfoDto.toYamlConfig
            |> Collections.Generic.List

        randomizeInfoListDto.Save randomizeInfoListDtoPath

    let reset (randomizeInfo: Domain<RandomizeInfoDto, RandomizeInfo, string>) =
        let origin = Config()
        let randomizeInfoListDtoOrigin = RandomizeInfoListDtoYaml()

        { Frames = frames.Create origin.Frames
          Duration = duration.Create origin.Duration
          Interval = interval.Create origin.Interval
          RandomizeInfoList =
            List.ofSeq randomizeInfoListDtoOrigin.RandomizeInfoDto
            |> List.map (
                RandomizeInfoDto.ofYamlConfig
                >> randomizeInfo.Create
            )
          PlayListFilePath = playListFilePath.Create origin.PlayListFilePath
          SnapShotFolderPath = snapShotFolderPath.Create origin.SnapShotFolderPath }

type Model =
    { Settings: Settings
      PickedPlayListPath: DeferredResult<string, FilePickerError>
      PickedSnapShotFolderPath: DeferredResult<string, FilePickerError> }

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
    | ResetSettings

type Api =
    { validateMediaInfo: RandomizeInfoDto -> Result<RandomizeInfoDto, string list>
      parsePlayListFile: PlayListFilePath -> Task<Result<RandomizeInfoDto list, string list>>
      pickPlayList: unit -> Task<Result<string, FilePickerError>>
      pickSnapshotFolder: unit -> Task<Result<string, FilePickerError>>
      showInfomation: NotifyMessage -> Async<unit> }

module ApiMock =
    let api =
        { validateMediaInfo = fun info -> Ok info
          parsePlayListFile = fun _ -> task { return Ok [ RandomizeInfoDto.mock ] }
          pickPlayList = fun _ -> task { return Ok "Test" }
          pickSnapshotFolder = fun _ -> task { return Ok "Foo" }
          showInfomation = fun _ -> async { () } }

open Elmish
open FsToolkit.ErrorHandling

type Cmds(api: Api) =

    let showInfomation info =
        task { do! api.showInfomation info } |> ignore

    let teeFileSystemError result =
        result
        |> TaskResult.teeError (sprintf "%A" >> ErrorMsg >> showInfomation)

    member _.randomizeInfo = initRandomizeInfoDomain api.validateMediaInfo

    member _.ShowInfomation info = showInfomation info

    member _.PickPlayList() =
        task {
            let! result = api.pickPlayList () |> teeFileSystemError
            return (Finished >> PickPlayList) result
        }
        |> Cmd.OfTask.result

    member _.PickSnapshotFolder() =
        task {
            let! result = api.pickSnapshotFolder () |> teeFileSystemError
            return (Finished >> PickSnapshotFolder) result
        }
        |> Cmd.OfTask.result

let init api =
    initRandomizeInfoDomain api.validateMediaInfo
    |> Settings.Default
    |> Model.create

let update (cmds: Cmds) msg (m: Model) =

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
        Settings.save cmds.randomizeInfo m.Settings
        m, Cmd.none
    | ResetSettings -> { m with Settings = Settings.reset cmds.randomizeInfo }, Cmd.none
