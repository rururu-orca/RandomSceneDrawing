module RandomSceneDrawing.Types

open System
open System.Threading.Tasks
open LibVLCSharp

[<Struct>]
type Validated<'value, 'wrapped, 'error> =
    private
    | Valid of wrapped: 'wrapped
    | Invalid of current: 'wrapped voption * arg: 'value * error: 'error

[<Struct>]
type UnwrapInvalid<'value, 'error> =
    { Current: 'value voption
      Arg: 'value
      Error: 'error }

let (|Valid|Invalid|) (validated: Validated<'value, 'wrapped, 'error>) =
    match validated with
    | Valid value -> Valid value
    | Invalid (current, arg, error) -> Invalid(current, arg, error)

type Validator<'value, 'wrapped, 'error>(wrap, unwrap, validate) =
    let validate: 'value -> Result<'value, 'error> = validate
    let wrap: 'value -> 'wrapped = wrap
    let unwrap: 'wrapped -> 'value = unwrap

    member _.Create value =
        match validate value with
        | Ok v -> Valid(wrap v)
        | Error error -> Invalid(ValueNone, value, error)

    member _.Update newValueArg (currentValue: Validated<'value, 'wrapped, 'error>) =
        match (validate newValueArg), currentValue with
        | Ok v, _ -> Valid(wrap v)
        | Error error, Valid c -> Invalid(ValueSome c, newValueArg, error)
        | Error error, Invalid _ -> Invalid(ValueNone, newValueArg, error)


    member _.Fold onValid onInvalid (validated: Validated<'value, 'wrapped, 'error>) =
        match validated with
        | Valid x -> onValid x
        | Invalid e -> onInvalid e

    member this.UnwrapFold onValid onInvalid (validated: Validated<'value, 'wrapped, 'error>) =
        this.Fold
            (unwrap >> onValid)
            ((fun (v, invalid, error) ->
                { Current = ValueOption.map unwrap v
                  Arg = invalid
                  Error = error })
             >> onInvalid)
            validated

    member this.Map (f: 'value -> 'value) validated =
        match validated with
        | Valid v -> validated |> this.Update(f (unwrap v))
        | invalid -> invalid

    member this.UnwrapWith (f: 'value -> 'a) (validated: Validated<'value, 'wrapped, 'error>) =
        this.UnwrapFold(f >> Ok) (id >> Error) validated

    member this.UnwrapOr f (validated: Validated<'value, 'wrapped, 'error>) =
        this.UnwrapFold(id >> Ok) (f >> Error) validated

    member this.Unwrap(validated: Validated<'value, 'wrapped, 'error>) = this.UnwrapWith id validated

    member this.ValueOr f (validated: Validated<'value, 'wrapped, 'error>) = this.UnwrapFold id f validated

    member this.DefaultWith f (validated: Validated<'value, 'wrapped, 'error>) = this.ValueOr(ignore >> f) validated

    member this.DefaultValue value (validated: Validated<'value, 'wrapped, 'error>) =
        this.DefaultWith(fun () -> value) validated

    member this.Tee f (validated: Validated<'value, 'wrapped, 'error>) = this.Fold f ignore validated

    member this.UnwrapTee f (validated: Validated<'value, 'wrapped, 'error>) = this.UnwrapFold f ignore validated

    member this.TeeInvalid f (validated: Validated<'value, 'wrapped, 'error>) = this.Fold ignore f validated

    member this.UnwrapTeeInvalid f (validated: Validated<'value, 'wrapped, 'error>) = this.UnwrapFold ignore f validated

    member _.IsValid(validated: Validated<'value, 'wrapped, 'error>) =
        match validated with
        | Valid _ -> true
        | Invalid _ -> false

    member _.IsInvalid(validated: Validated<'value, 'wrapped, 'error>) =
        match validated with
        | Valid _ -> false
        | Invalid _ -> true


module ErrorTypes =
    type FilePickerError =
        | Canceled
        | FileSystemError of string

module Validator =
    let validateIfPositiveNumber num =
        if num < 0 then
            Error "Must be a positive number."
        else
            Ok num

    let validateIfPositiveTime time =
        if time < TimeSpan.Zero then
            Error "Must be a positive number."
        else
            Ok time

    open System.IO

    type FileType =
        | File
        | Directory

    let validateExists label path =
        match label with
        | File when File.Exists path -> Ok path
        | Directory when Directory.Exists path -> Ok path
        | _ -> Error $"{path} is not exsists."

    let validatePathString label path =
        if String.IsNullOrEmpty path then
            Ok path
        else
            validateExists label path

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
