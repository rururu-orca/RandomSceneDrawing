module RandomSceneDrawing.Types

open System
open System.Windows
open LibVLCSharp.Shared
open System.Windows.Input

type State =
    | Stop
    | Running

type MediaInfo = {
    Title: string
    Duration: TimeSpan }

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
    | PlaySuccess of MediaInfo
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

type AppViewModel =
    { MediaPlayer: MediaPlayer
      ScenePosition: float
      SourceDuration: float
      SourceName: string
      Play: ICommand
      Pause: ICommand
      Stop: ICommand
      FramesText: string
      IncrementFrames: ICommand
      DecrementFrames: ICommand
      DurationText: string
      IncrementDuration: ICommand
      DecrementDuration: ICommand
      Randomize: ICommand
      CurrentDuration: string
      CurrentFrames: int
      Position: int
      DrawingServiceVisibility: Visibility }
