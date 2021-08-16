module RandomSceneDrawing.Types

open System
open System.Windows
open LibVLCSharp.Shared
open System.Windows.Input

type RandomDrawingState =
    | Stop
    | Running
    | Interval

type PlayerState =
    | Playing
    | Paused
    | Stopped
    | Randomizung

type MediaInfo = {
    Title: string
    Duration: TimeSpan }

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: TimeSpan
      Player: MediaPlayer
      PlayerState : PlayerState
      MediaDuration: TimeSpan
      MediaPosition: TimeSpan
      PlayListFilePath: string
      SnapShotFolderPath: string
      Title: string
      RandomDrawingState: RandomDrawingState
      CurrentDuration: TimeSpan
      CurrentFrames: int 
      StatusMessage : string}

type CmdMsg =
    | Play
    | Pause
    | Stop
    | Randomize
    | StartDrawing
    | StopDrawing
    | SelectPlayListFilePath
    | SelectSnapShotFolderPath

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
    | PlayerTimeChanged of TimeSpan
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
    | IncrementFrames
    | DecrementFrames
    | SetDuration of TimeSpan
    | IncrementDuration
    | DecrementDuration
    | LayoutUpdated of  string
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
    | WindowClosed
    | ResetSettings

type AppViewModel =
    { MediaPlayer: MediaPlayer
      ScenePosition: float
      SourceDuration: float
      SourceName: string
      Play: ICommand
      Pause: ICommand
      Stop: ICommand
      mutable FramesText: string
      IncrementFrames: ICommand
      DecrementFrames: ICommand
      mutable DurationText: string
      IncrementDuration: ICommand
      DecrementDuration: ICommand
      PlayListFilePathText: string
      SnapShotFolderPathText: string
      Randomize: ICommand
      DrawingCommand : ICommand
      DrawingCommandText : String
      State : RandomDrawingState
      CurrentDuration: string
      CurrentFrames: int
      Position: int
      DrawingServiceVisibility: Visibility
      DrawingSettingVisibility: Visibility
      WindowClosed: ICommand }
