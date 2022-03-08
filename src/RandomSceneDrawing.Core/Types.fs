module RandomSceneDrawing.Types

open System
open System.Threading.Tasks
open LibVLCSharp


type AsyncOperationStatus<'t> =
    | Started
    | Finished of 't

type Deferred<'t> =
    | HasNotStartedYet
    | InProgress
    | Resolved of 't


/// Contains utility functions to work with value of the type `Deferred<'T>`.
module Deferred =

    /// Returns whether the `Deferred<'T>` value has been resolved or not.
    let resolved =
        function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved _ -> true

    /// Returns whether the `Deferred<'T>` value is in progress or not.
    let inProgress =
        function
        | HasNotStartedYet -> false
        | InProgress -> true
        | Resolved _ -> false

    /// Transforms the underlying value of the input deferred value when it exists from type to another
    let map (transform: 'T -> 'U) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> Resolved(transform value)

    /// Verifies that a `Deferred<'T>` value is resolved and the resolved data satisfies a given requirement.
    let exists (predicate: 'T -> bool) =
        function
        | HasNotStartedYet -> false
        | InProgress -> false
        | Resolved value -> predicate value

    /// Like `map` but instead of transforming just the value into another type in the `Resolved` case, it will transform the value into potentially a different case of the the `Deferred<'T>` type.
    let bind (transform: 'T -> Deferred<'U>) (deferred: Deferred<'T>) : Deferred<'U> =
        match deferred with
        | HasNotStartedYet -> HasNotStartedYet
        | InProgress -> InProgress
        | Resolved value -> transform value

type RandomDrawingState =
    | Stop
    | Running
    | Interval

type CommandState =
    | Waiting
    | Running
    | WaitBuffering

type PlayerState =
    | Playing
    | Paused
    | Stopped

exception PlayFailedException of string
exception SnapShotFailedException of string

type ValidateError =
    | OverUpperLimit
    | BelowLowerLimit

type MediaInfo = { Title: string; Duration: TimeSpan }

module Player =
    type State =
        | Playing
        | Paused
        | Stopped

    type Model<'player> =
        { Player: 'player
          State: AsyncOperationStatus<Result<State, string>>
          Media: Deferred<Result<MediaInfo, string>> }

    type Msg =
        | Play of Result<MediaInfo, string> AsyncOperationStatus
        | Pause of Result<MediaInfo, string> AsyncOperationStatus
        | Stop of Result<unit, string> AsyncOperationStatus

    type api<'player> =
        { playAsync: 'player -> Task<Msg>
          pauseAsync: 'player -> Task<Msg>
          stopAsync: 'player -> Task<Msg> }

    open Elmish
    open Elmish.Cmd.OfTask

    let init player =
        { Player = player
          State = Finished(Ok Stopped)
          Media = HasNotStartedYet }

    let update api msg m =
        let mapState state = Result.map (fun _ -> state)

        match msg, m.Media with
        | Play Started, InProgress -> m, Cmd.none
        | Play Started, _ ->
            { m with
                Media = InProgress
                State = Started },
            api.playAsync m.Player |> result
        | Play (Finished result), _ ->
            { m with
                Media = Resolved result
                State = Finished(mapState Playing result) },
            Cmd.none
        | Pause Started, HasNotStartedYet
        | Pause Started, InProgress
        | Pause Started, Resolved (Error _) -> m, Cmd.none
        | Pause Started, _ ->
            { m with
                State = Started },
            api.pauseAsync m.Player |> result
        | Pause (Finished (Ok mediainfo)), _ ->
            { m with
                Media = Resolved(Ok mediainfo)
                State = Finished(Ok Paused) },
            Cmd.none
        | Pause (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none
        | Stop Started, HasNotStartedYet
        | Stop Started, InProgress
        | Stop Started, Resolved (Error _) -> m, Cmd.none
        | Stop Started, _ ->
            { m with
                State = Started },
            api.stopAsync m.Player |> result
        | Stop (Finished (Ok _)), _ ->
            { m with
                Media = HasNotStartedYet
                State = Finished(Ok Stopped) },
            Cmd.none
        | Stop (Finished (Error msg)), _ -> { m with State = Finished(Error msg) }, Cmd.none




module Main =
    open Elmish

    type PlayerId =
        | MainPlayer
        | SubPlayer

    type Model<'player> =
        { MainPlayer: Player.Model<'player>
          SubPlayer: Player.Model<'player> }

    type Msg = PlayerMsg of PlayerId * Player.Msg


    let init player subPlayer =
        { MainPlayer = Player.init player
          SubPlayer = Player.init subPlayer }

    let update playerApi msg m =
        let playerUpdate = Player.update playerApi

        match msg with
        | PlayerMsg (MainPlayer, msg) ->
            let mainPlayer', cmd' = playerUpdate msg m.MainPlayer

            { m with MainPlayer = mainPlayer' }, Cmd.map ((fun m -> MainPlayer, m) >> PlayerMsg) cmd'

        | PlayerMsg (SubPlayer, msg) ->
            let player', cmd' = playerUpdate msg m.SubPlayer

            { m with SubPlayer = player' }, Cmd.map ((fun m -> SubPlayer, m) >> PlayerMsg) cmd'


type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: TimeSpan
      Player: MediaPlayer
      PlayerMediaInfo: Result<MediaInfo, string> Deferred
      SubPlayer: MediaPlayer
      PlayerState: PlayerState
      PlayerBufferCache: float32
      RandomizeState: CommandState
      MediaDuration: TimeSpan
      MediaPosition: TimeSpan
      PlayListFilePath: string
      SnapShotFolderPath: string
      SnapShotPath: string
      Title: string
      RandomDrawingState: RandomDrawingState
      CurrentDuration: TimeSpan
      CurrentFrames: int
      StatusMessage: string }

type CmdMsg =
    | Play of MediaPlayer
    | Pause of MediaPlayer
    | Stop of MediaPlayer
    | Randomize of MediaPlayer * MediaPlayer * string
    | CreateCurrentSnapShotFolder of string
    | TakeSnapshot of MediaPlayer * string
    | StartDrawing
    | StopDrawing
    | SelectPlayListFilePath
    | SelectSnapShotFolderPath
    | ShowErrorInfomation of string

type Msg =
    | Play of Result<MediaInfo, string> AsyncOperationStatus
    | RequestPause
    | PauseSuccess of PlayerState
    | PauseFailed of exn
    | RequestStop
    | StopSuccess
    | StopFailed of exn
    | PlayerTimeChanged of TimeSpan
    | PlayerBuffering of float32
    | RequestRandomize
    | RandomizeSuccess
    | RandomizeFailed of exn
    | RequestStartDrawing
    | StartDrawingSuccess
    | StartDrawingFailed of exn
    | Tick
    | RequestStopDrawing
    | StopDrawingSuccess
    | SetFrames of int
    | IncrementFrames of int
    | DecrementFrames of int
    | SetDuration of TimeSpan
    | IncrementDuration of TimeSpan
    | DecrementDuration of TimeSpan
    | LayoutUpdated of string
    | SetPlayListFilePath of string
    | RequestSelectPlayListFilePath
    | SelectPlayListFilePathSuccess of string
    | SelectPlayListFilePathCanceled
    | SelectPlayListFilePathFailed of exn
    | SetSnapShotFolderPath of string
    | RequestSelectSnapShotFolderPath
    | SelectSnapShotFolderPathSuccess of string
    | SelectSnapShotFolderPathCandeled
    | SelectSnapShotFolderPathFailed of exn
    | CreateCurrentSnapShotFolderSuccess of string
    | TakeSnapshotSuccess
    | TakeSnapshotFailed of exn
    | WindowClosed
    | ResetSettings
    | ShowErrorInfomationSuccess


open Elmish

type Api =
    { playAsync: MediaPlayer -> Task<Msg>
      pauseAsync: MediaPlayer -> Task<Msg>
      stopAsync: MediaPlayer -> Task<Msg>
      randomizeAsync: MediaPlayer -> MediaPlayer -> string -> Task<Msg>
      createCurrentSnapShotFolderAsync: string -> Task<Msg>
      takeSnapshotAsync: MediaPlayer * string -> Task<Msg>
      startDrawing: unit -> Cmd<Msg>
      stopDrawingAsync: unit -> Task<Msg>
      selectPlayListFilePathAsync: unit -> Task<Msg>
      selectSnapShotFolderPathAsync: unit -> Task<Msg>
      showErrorAsync: string -> Task<Msg> }
