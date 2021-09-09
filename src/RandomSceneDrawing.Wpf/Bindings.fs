module RandomSceneDrawing.Bindings

open System
open System.Windows.Input
open Microsoft.FSharp.Reflection
open Elmish.WPF
open LibVLCSharp.Shared
open RandomSceneDrawing.Types
open RandomSceneDrawing.Program
open System.Windows


let createCommand action canExecute =
    let event1 = Event<_, _>()

    { new ICommand with
        member this.CanExecute(obj) = canExecute (obj)
        member this.Execute(obj) = action (obj)
        member this.add_CanExecuteChanged(handler) = event1.Publish.AddHandler(handler)
        member this.remove_CanExecuteChanged(handler) = event1.Publish.AddHandler(handler) }

let emptyCommand = createCommand (ignore) (fun _ -> true)

type BindingLabel =
    | MediaPlayer
    | ScenePosition
    | SourceDuration
    | SourceName
    | MediaPlayerVisibility
    | MediaBlindVisibility
    | PlayCommand
    | PauseCommand
    | StopCommand
    | FramesText
    | IncrementFramesCommand
    | DecrementFramesCommand
    | DurationText
    | IncrementDurationCommand
    | DecrementDurationCommand
    | PlayListFilePathText
    | SetPlayListFilePathCommand
    | SnapShotFolderPathText
    | SetSnapShotFolderPathCommand
    | RandomizeCommand
    | CurrentDuration
    | CurrentFrames
    | DrawingCommand
    | DrawingCommandText
    | DrawingSettingVisibility
    | DrawingServiceVisibility
    | StatusMessage
    | WindowTopUpdatedCommand
    | WindowLeftUpdatedCommand
    | WindowClosedCommand

    static member GetLabelAndCaseSeq() =
        // Get all cases of the union
        FSharpType.GetUnionCases(typeof<BindingLabel>)
        |> Seq.map (fun c -> c.Name, FSharpValue.MakeUnion(c, [||]) :?> BindingLabel)
        |> Seq.toList

type DesignVm =
    { MediaPlayer: MediaPlayer
      ScenePosition: string
      SourceDuration: string
      SourceName: string
      MediaPlayerVisibility: Visibility
      MediaBlindVisibility: Visibility
      PlayCommand: ICommand
      PauseCommand: ICommand
      StopCommand: ICommand
      mutable FramesText: string
      IncrementFramesCommand: ICommand
      DecrementFramesCommand: ICommand
      mutable DurationText: string
      IncrementDurationCommand: ICommand
      DecrementDurationCommand: ICommand
      mutable PlayListFilePathText: string
      SetPlayListFilePathCommand: ICommand
      mutable SnapShotFolderPathText: string
      SetSnapShotFolderPathCommand: ICommand
      RandomizeCommand: ICommand
      CurrentDuration: int
      CurrentFrames: int
      DrawingCommand: ICommand
      DrawingCommandText: string
      DrawingSettingVisibility: Visibility
      DrawingServiceVisibility: Visibility
      StatusMessage: string
      WindowTopUpdatedCommand: ICommand
      WindowLeftUpdatedCommand: ICommand
      WindowClosedCommand: ICommand }

let designVm =
    do PlayerLib.initialize ()

    { MediaPlayer = PlayerLib.initPlayer ()
      ScenePosition = "00:00:00"
      SourceDuration = "00:00:00"
      SourceName = ""
      MediaPlayerVisibility = Visibility.Collapsed
      MediaBlindVisibility = Visibility.Collapsed
      PlayCommand = emptyCommand
      PauseCommand = emptyCommand
      StopCommand = emptyCommand
      FramesText = "0"
      IncrementFramesCommand = emptyCommand
      DecrementFramesCommand = emptyCommand
      DurationText = "00:00"
      IncrementDurationCommand = emptyCommand
      DecrementDurationCommand = emptyCommand
      PlayListFilePathText = "C:/Path/To/PlayList"
      SetPlayListFilePathCommand = emptyCommand
      SnapShotFolderPathText = "C:/Path/To/SnapShot"
      SetSnapShotFolderPathCommand = emptyCommand
      RandomizeCommand = emptyCommand
      CurrentDuration = 0
      CurrentFrames = 0
      DrawingCommand = emptyCommand
      DrawingCommandText = "⏲ Start Drawing"
      DrawingSettingVisibility = Visibility.Visible
      DrawingServiceVisibility = Visibility.Collapsed
      StatusMessage = "Status"
      WindowTopUpdatedCommand = emptyCommand
      WindowLeftUpdatedCommand = emptyCommand
      WindowClosedCommand = emptyCommand }

let bindingsMapper (name, label) =
    match label with
    | MediaPlayer -> Binding.oneWay (fun m -> m.Player)
    | ScenePosition -> Binding.oneWay (fun m -> m.MediaPosition.ToString @"hh\:mm\:ss")
    | SourceDuration -> Binding.oneWay (fun m -> m.MediaDuration.ToString @"hh\:mm\:ss")
    | SourceName -> Binding.oneWay (fun m -> m.Title)
    | MediaPlayerVisibility ->
        Binding.oneWay
            (fun m ->
                match m with
                | { RandomDrawingState = Interval }
                | { RandomizeState = Running }
                | { RandomizeState = WaitBuffering }
                | { PlayerState = Stopped } -> Visibility.Collapsed
                | { PlayerState = Playing }
                | { PlayerState = Paused } -> Visibility.Visible)
    | MediaBlindVisibility ->
        Binding.oneWay
            (fun m ->
                match m with
                | { RandomDrawingState = Interval }
                | { RandomizeState = Running }
                | { RandomizeState = WaitBuffering }
                | { PlayerState = Stopped } -> Visibility.Visible
                | { PlayerState = Playing }
                | { PlayerState = Paused } -> Visibility.Collapsed)
    | PlayCommand -> Binding.cmd RequestPlay
    | PauseCommand -> Binding.cmdIf (RequestPause, (fun m -> m.PlayerState <> Stopped))
    | StopCommand -> Binding.cmdIf (RequestStop, (fun m -> m.PlayerState <> Stopped))
    | FramesText -> Binding.twoWay ((fun m -> string m.Frames), (int >> SetFrames))
    | IncrementFramesCommand -> Binding.cmd IncrementFrames
    | DecrementFramesCommand -> Binding.cmdIf (DecrementFrames, (requireGreaterThan1Frame >> mapCanExec))
    | DurationText ->
        Binding.twoWay ((fun m -> m.Duration.ToString @"mm\:ss"), (TimeSpan.Parse >> SetDuration))
        >> Binding.withValidation requireDurationGreaterThan
    | IncrementDurationCommand -> Binding.cmd IncrementDuration
    | DecrementDurationCommand -> Binding.cmdIf (DecrementDuration, (requireDurationGreaterThan >> mapCanExec))
    | PlayListFilePathText -> Binding.twoWay ((fun m -> string m.PlayListFilePath), (string >> SetPlayListFilePath))
    | SetPlayListFilePathCommand -> Binding.cmd RequestSelectPlayListFilePath
    | SnapShotFolderPathText ->
        Binding.twoWay ((fun m -> string m.SnapShotFolderPath), (string >> SetSnapShotFolderPath))
    | SetSnapShotFolderPathCommand -> Binding.cmd RequestSelectSnapShotFolderPath
    | RandomizeCommand ->
        Binding.cmdIf (
            RequestRandomize,
            (fun m ->
                m.RandomizeState = Waiting
                && not (String.IsNullOrEmpty m.PlayListFilePath))
        )
    | CurrentDuration -> Binding.oneWay (fun m -> m.CurrentDuration)
    | CurrentFrames -> Binding.oneWay (fun m -> m.CurrentFrames)
    | DrawingCommand ->
        Binding.cmdIf
            (fun (m: Model) ->
                if
                    not (String.IsNullOrEmpty m.PlayListFilePath)
                    && not (String.IsNullOrEmpty m.SnapShotFolderPath)
                then
                    match m.RandomDrawingState with
                    | RandomDrawingState.Stop -> Some RequestStartDrawing
                    | RandomDrawingState.Running
                    | Interval -> Some RequestStopDrawing
                else
                    None)
    | DrawingCommandText ->
        Binding.oneWay
            (fun m ->
                match m.RandomDrawingState with
                | RandomDrawingState.Stop -> "⏲ Start Drawing"
                | RandomDrawingState.Running
                | Interval -> "Stop Drawing")
    | DrawingSettingVisibility ->
        Binding.oneWay
            (fun m ->
                match m.RandomDrawingState with
                | RandomDrawingState.Stop -> Visibility.Visible
                | RandomDrawingState.Running
                | Interval -> Visibility.Collapsed)
    | DrawingServiceVisibility ->
        Binding.oneWay
            (fun m ->
                match m.RandomDrawingState with
                | RandomDrawingState.Stop -> Visibility.Collapsed
                | RandomDrawingState.Running
                | Interval -> Visibility.Visible)
    | StatusMessage -> Binding.oneWay (fun m -> m.StatusMessage)
    | WindowTopUpdatedCommand ->
        Binding.cmdParam
            (fun p ->
                let args = p :?> float
                LayoutUpdated $"WIndow Top:{args}")
    | WindowLeftUpdatedCommand ->
        Binding.cmdParam
            (fun p ->
                let args = p :?> float
                LayoutUpdated $"WIndow Left:{args}")
    | WindowClosedCommand -> Binding.cmd WindowClosed
    <| name

let bindings () =
    BindingLabel.GetLabelAndCaseSeq()
    |> List.map bindingsMapper
