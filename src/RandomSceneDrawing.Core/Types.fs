module RandomSceneDrawing.Types

open System
open System.Windows
open LibVLCSharp.Shared

type State =
    | Stop
    | Running

type MediaIndo = {
    Duration : TimeSpan
}

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: int
      DrawingServiceVisibility: Visibility
      Player: MediaPlayer
      MediaDuration: TimeSpan
      MediaPosition: float
      Title: string
      State: State
      CurrentDuration: TimeSpan
      CurrentFrames: int }

type CmdMsg =
    | Play
    | Pause
    | Stop
    | Randomize
    | StartDrawing

type Msg =
    | RequestPlay
    | PlaySuccess of MediaIndo
    | PlayFailed of VLCState
    | RequestPause
    | PauseSuccess
    | PauseFailed of exn
    | RequestStop
    | StopSuccess
    | StopFailed of exn
    | RequestRandomize
    | RandomizeSuccess of unit
    | RandomizeFailed of exn
    | SetFrames of int
    | IncrementFrames
    | DecrementFrames
    | SetDuration of TimeSpan
    | IncrementDuration
    | DecrementDuration