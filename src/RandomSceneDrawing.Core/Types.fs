module RandomSceneDrawing.Types

open System
open System.Threading.Tasks
open LibVLCSharp

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
    | RequestPlay
    | PlaySuccess of MediaInfo
    | PlayCandeled
    | PlayFailed of exn
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
