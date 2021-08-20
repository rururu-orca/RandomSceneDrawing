module RandomSceneDrawing.Program

open System
open System.IO
open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open FSharp.Configuration
open Types
open RandomSceneDrawing
open FSharpPlus

type Config = YamlConfig<"Config.yaml">

let changedConfigPath =
    Path.Combine [| AppDomain.CurrentDomain.BaseDirectory
                    "ChangedConfig.yaml" |]

let config = Config()

let init () =
    if File.Exists changedConfigPath then
        config.Load changedConfigPath

    { Frames = config.Frames
      Duration = config.Duration
      Interval = config.Interval
      Player = PlayerLib.player
      PlayerState = PlayerState.Stopped
      MediaDuration = TimeSpan.Zero
      MediaPosition = TimeSpan.Zero
      PlayerBufferCache = 0.0f
      RandomizeState = Waiting
      PlayListFilePath = config.PlayListFilePath
      SnapShotFolderPath = config.SnapShotFolderPath
      SnapShotPath = ""
      Title = ""
      RandomDrawingState = RandomDrawingState.Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0
      StatusMessage = "" },
    []

let requireGreaterThan1Frame input =
    [ if input.Frames < 1 then
          $"Frames must greater than 1" ]

let requireDurationGreaterThan input =
    let ts = TimeSpan(0, 0, 1)

    [ if input.Duration < ts then
          $"Frames must greater than {ts}" ]

let mapCanExec =
    function
    | [] -> true
    | _ -> false



let update msg m =
    match msg with
    // Player
    | RequestPlay -> m, [ Play ]
    | PlayCandeled -> m, []
    | PlaySuccess mediaInfo ->
        { m with
              Title = mediaInfo.Title
              PlayerState = Playing
              MediaDuration = mediaInfo.Duration },
        []
    | PlayFailed e ->
        { m with
              StatusMessage = sprintf "%A" e },
        []
    | RequestPause -> m, [ Pause ]
    | PauseSuccess -> m, []
    | PauseFailed _ -> failwith "Not Implemented"
    | RequestStop -> m, [ Stop ]
    | StopSuccess -> { m with PlayerState = Stopped }, []
    | StopFailed _ -> failwith "Not Implemented"
    | PlayerTimeChanged time -> { m with MediaPosition = time }, []
    | PlayerBuffering cache ->
        match cache with
        | 100.0f when m.RandomizeState = WaitBuffering ->
            { m with
                  PlayerBufferCache = cache
                  RandomizeState = Waiting },
            []
        | _ -> { m with PlayerBufferCache = cache }, []

    // Random Drawing Setting
    | SetFrames x -> { m with Frames = x }, []
    | IncrementFrames -> { m with Frames = m.Frames + 1 }, []
    | DecrementFrames -> { m with Frames = m.Frames - 1 }, []
    | SetDuration x -> { m with Duration = x }, []
    | IncrementDuration ->
        { m with
              Duration = m.Duration.Add <| TimeSpan.FromSeconds 10.0 },
        []
    | DecrementDuration ->
        { m with
              Duration = m.Duration.Add <| TimeSpan.FromSeconds -10.0 },
        []
    | SetPlayListFilePath path -> { m with PlayListFilePath = path }, []
    | RequestSelectPlayListFilePath -> m, [ SelectPlayListFilePath ]
    | SelectPlayListFilePathSuccess path -> { m with PlayListFilePath = path }, []
    | SelectPlayListFilePathCanceled _ -> failwith "Not Implemented"
    | SelectPlayListFilePathFailed ex -> failwith "Not Implemented"

    | SetSnapShotFolderPath path -> { m with SnapShotFolderPath = path }, []
    | RequestSelectSnapShotFolderPath -> m, [ SelectSnapShotFolderPath ]
    | SelectSnapShotFolderPathSuccess path -> { m with SnapShotFolderPath = path }, []
    | SelectSnapShotFolderPathCandeled -> failwith "Not Implemented"
    | SelectSnapShotFolderPathFailed ex -> failwith "Not Implemented"

    // Random Drawing
    | RequestRandomize (_) -> { m with RandomizeState = Running }, [ Randomize m.PlayListFilePath ]
    | RandomizeSuccess (_) ->
        { m with
              Title = m.Player.Media.Meta LibVLCSharp.Shared.MetadataType.Title
              PlayerState = Playing
              RandomizeState = if m.PlayerBufferCache = 100.0f then Waiting else WaitBuffering
              MediaPosition = (float m.Player.Time |> TimeSpan.FromMilliseconds)
              MediaDuration = (float m.Player.Length |> TimeSpan.FromMilliseconds) },
        [ if m.RandomDrawingState = Interval then
              let path =
                  Path.Combine [| m.SnapShotPath
                                  $"%03i{m.CurrentFrames}.png" |]

              TakeSnapshot path ]
    | RandomizeFailed ex ->
        match ex with
        | :? PlayerLib.PlayFailedException -> { m with RandomizeState = Waiting }, [ Stop ]
        | _ -> { m with PlayerState = Stopped }, [ Stop; Randomize m.PlayListFilePath ]
    | RequestStartDrawing (_) ->
        m,
        [ CreateCurrentSnapShotFolder m.SnapShotFolderPath
          StartDrawing ]
    | RequestStopDrawing (_) -> m, [ StopDrawing ]
    | StartDrawingSuccess (_) ->
        { m with
              CurrentFrames = 1
              CurrentDuration = m.Interval
              RandomDrawingState = Interval
              RandomizeState = Running },
        [ Randomize m.PlayListFilePath ]
    | CreateCurrentSnapShotFolderSuccess path -> { m with SnapShotPath = path }, []
    | StartDrawingFailed (_) -> failwith "Not Implemented"
    | StopDrawingSuccess ->
        { m with
              RandomDrawingState = RandomDrawingState.Stop },
        []
    | Tick ->
        let nextDuration = m.CurrentDuration - TimeSpan(0, 0, 1)

        if nextDuration > TimeSpan.Zero then
            { m with
                  CurrentDuration = m.CurrentDuration - TimeSpan(0, 0, 1) },
            []
        elif m.RandomDrawingState = Interval then
            { m with
                  RandomDrawingState = RandomDrawingState.Running
                  CurrentDuration = m.Duration },
            []
        elif m.CurrentFrames < m.Frames then
            { m with
                  RandomDrawingState = Interval
                  RandomizeState = Running
                  CurrentFrames = m.CurrentFrames + 1
                  CurrentDuration = m.Interval },
            [ Randomize m.PlayListFilePath ]
        else
            { m with
                  CurrentDuration = TimeSpan.Zero },
            [ StopDrawing ]
    | TakeSnapshotSuccess
    | TakeSnapshotFailed -> m, []
    // Util
    | LayoutUpdated p -> { m with StatusMessage = p }, []
    | WindowClosed ->
        config.Duration <- m.Duration
        config.Frames <- m.Frames
        config.Interval <- m.Interval
        config.PlayListFilePath <- m.PlayListFilePath
        config.SnapShotFolderPath <- m.SnapShotFolderPath
        config.Save changedConfigPath
        m, []
    | ResetSettings ->
        let origin = Config()

        { m with
              Duration = origin.Duration
              Frames = origin.Frames
              Interval = origin.Interval
              PlayListFilePath = origin.PlayListFilePath
              SnapShotFolderPath = origin.SnapShotFolderPath },
        []



let bindings () =
    [
      // Player
      "MediaPlayer"
      |> Binding.oneWay (fun m -> m.Player)
      "ScenePosition"
      |> Binding.oneWay (fun m -> m.MediaPosition.ToString @"hh\:mm\:ss")
      "SourceDuration"
      |> Binding.oneWay (fun m -> m.MediaDuration.ToString @"hh\:mm\:ss")
      "SourceName" |> Binding.oneWay (fun m -> m.Title)
      "MediaPlayerVisibility"
      |> Binding.oneWay
          (fun m ->
              match m with
              | { RandomDrawingState = Interval }
              | { RandomizeState = Running }
              | { RandomizeState = WaitBuffering }
              | { PlayerState = Stopped } -> Visibility.Collapsed
              | { PlayerState = Playing }
              | { PlayerState = Paused } -> Visibility.Visible)

      "Pause" |> Binding.cmd RequestPause
      "Play" |> Binding.cmd RequestPlay
      "Stop" |> Binding.cmd RequestStop

      // Random Drawing Setting
      "FramesText"
      |> Binding.twoWay ((fun m -> string m.Frames), (int >> SetFrames))
      |> Binding.withValidation requireGreaterThan1Frame
      "IncrementFrames" |> Binding.cmd IncrementFrames
      "DecrementFrames"
      |> Binding.cmdIf (DecrementFrames, (requireGreaterThan1Frame >> mapCanExec))

      "DurationText"
      |> Binding.twoWay ((fun m -> m.Duration.ToString @"mm\:ss"), (TimeSpan.Parse >> SetDuration))
      |> Binding.withValidation requireDurationGreaterThan
      "IncrementDuration"
      |> Binding.cmd IncrementDuration
      "DecrementDuration"
      |> Binding.cmdIf (DecrementDuration, (requireDurationGreaterThan >> mapCanExec))

      "PlayListFilePathText"
      |> Binding.twoWay ((fun m -> string m.PlayListFilePath), (string >> SetPlayListFilePath))
      "SetPlayListFilePath"
      |> Binding.cmd RequestSelectPlayListFilePath

      "SnapShotFolderPathText"
      |> Binding.twoWay ((fun m -> string m.SnapShotFolderPath), (string >> SetSnapShotFolderPath))
      "SetSnapShotFolderPath"
      |> Binding.cmd RequestSelectSnapShotFolderPath

      // Random Drawing
      "Randomize"
      |> Binding.cmdIf (
          RequestRandomize,
          (fun m ->
              m.RandomizeState = Waiting
              && not (String.IsNullOrEmpty m.PlayListFilePath))
      )
      "CurrentDuration"
      |> Binding.oneWay (fun m -> m.CurrentDuration)
      "CurrentFrames"
      |> Binding.oneWay (fun m -> m.CurrentFrames)

      "DrawingCommand"
      |> Binding.cmdIf
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

      "DrawingCommandText"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> "⏲ Start Drawing"
              | RandomDrawingState.Running
              | Interval -> "Stop Drawing")
      "DrawingSettingVisibility"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> Visibility.Visible
              | RandomDrawingState.Running
              | Interval -> Visibility.Collapsed)

      "DrawingServiceVisibility"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> Visibility.Collapsed
              | RandomDrawingState.Running
              | Interval -> Visibility.Visible)
      "StatusMessage"
      |> Binding.oneWay (fun m -> m.StatusMessage)
      "WindowTopUpdated"
      |> Binding.cmdParam
          (fun p ->
              let args = p :?> float
              LayoutUpdated $"WIndow Top:{args}")
      "WindowLeftUpdated"
      |> Binding.cmdParam
          (fun p ->
              let args = p :?> float
              LayoutUpdated $"WIndow Left:{args}")
      "WindowClosed" |> Binding.cmd WindowClosed ]


module Platform =
    open Windows.Foundation
    open Windows.UI.Popups
    open Windows.Storage.Pickers
    open WinRT.Interop
    open FSharp.Control
    open System.Collections.Generic

    type AsyncBuilder with
        member x.Bind(t: IAsyncOperation<'T>, f: 'T -> Async<'R>) : Async<'R> =
            async.Bind(t.AsTask() |> Async.AwaitTask, f)


    let sprintfDateTime format (datetime: DateTime) = datetime.ToString(format = format)

    let sprintfNow format = DateTime.Now |> sprintfDateTime format

    let playSelectedVideo hwnd =
        async {
            let picker =
                FileOpenPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.VideosLibrary)


            InitializeWithWindow.Initialize(picker, hwnd)

            [ ".mp4"; ".mkv" ]
            |> List.iter picker.FileTypeFilter.Add

            match! picker.PickSingleFileAsync() with
            | null -> return PlayCandeled
            | file when String.IsNullOrEmpty file.Path ->
                let dlg =
                    MessageDialog("メディアサーバーの動画を指定して再生することは出来ません。", CancelCommandIndex = 0u)

                UICommand "Close" |> dlg.Commands.Add

                InitializeWithWindow.Initialize(dlg, hwnd)
                let! _ = dlg.ShowAsync()

                return PlayCandeled

            | file ->
                return!
                    PlayerLib.getMediaFromUri (Uri file.Path)
                    |> PlayerLib.play
        }

    let selectPlayList hwnd =
        async {
            let picker =
                FileOpenPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.MusicLibrary)

            InitializeWithWindow.Initialize(picker, hwnd)

            picker.FileTypeFilter.Add ".xspf"

            match! picker.PickSingleFileAsync() with
            | null -> return SelectSnapShotFolderPathCandeled
            | file -> return SelectPlayListFilePathSuccess file.Path
        }

    let selectSnapShotFolder hwnd =
        async {
            let picker =
                FolderPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.PicturesLibrary)

            InitializeWithWindow.Initialize(picker, hwnd)

            picker.FileTypeFilter.Add "*"

            match! picker.PickSingleFolderAsync() with
            | null -> return SelectSnapShotFolderPathCandeled
            | folder -> return SelectSnapShotFolderPathSuccess folder.Path
        }

    let createCurrentSnapShotFolder root =
        let unfolder state =
            match state with
            | -1 -> None
            | _ ->
                let path =
                    [| root
                       sprintfNow "yyyyMMdd"
                       $"%03i{state}" |]
                    |> Path.Combine

                match Directory.Exists path with
                | true -> Some(path, state + 1)
                | false ->
                    Directory.CreateDirectory path |> ignore
                    Some(path, -1)

        Seq.unfold unfolder 0
        |> Seq.last
        |> CreateCurrentSnapShotFolderSuccess


let toCmd hwnd =
    function
    // Player
    | Play ->
        Platform.playSelectedVideo hwnd
        |> Cmd.OfAsyncImmediate.result
    | Pause -> Cmd.OfAsyncImmediate.either PlayerLib.pause () id PauseFailed
    | Stop -> Cmd.OfAsyncImmediate.either PlayerLib.stop () id StopFailed

    | SelectPlayListFilePath -> Cmd.OfAsync.either Platform.selectPlayList hwnd id SelectPlayListFilePathFailed
    | SelectSnapShotFolderPath ->
        Cmd.OfAsync.either Platform.selectSnapShotFolder hwnd id SelectSnapShotFolderPathFailed
    // Random Drawing
    | Randomize pl -> 
        Cmd.OfAsyncImmediate.either PlayerLib.randomize (Uri pl) id RandomizeFailed
    | StartDrawing -> Cmd.OfFunc.either DrawingSetvice.tickSub StartDrawingSuccess id StartDrawingFailed
    | StopDrawing -> Cmd.OfFunc.result <| DrawingSetvice.stop ()
    | CreateCurrentSnapShotFolder root ->
        Platform.createCurrentSnapShotFolder root
        |> Cmd.ofMsg
    | TakeSnapshot path ->
        match PlayerLib.takeSnapshot PlayerLib.getSize 0u path with
        | Some path -> TakeSnapshotSuccess
        | None -> TakeSnapshotFailed
        |> Cmd.ofMsg



let designVm =
    { MediaPlayer = PlayerLib.player
      ScenePosition = 0.0
      SourceDuration = 0.0
      SourceName = ""
      Play = WpfHelper.emptyCommand
      Pause = WpfHelper.emptyCommand
      Stop = WpfHelper.emptyCommand
      FramesText = "0"
      IncrementFrames = WpfHelper.emptyCommand
      DecrementFrames = WpfHelper.emptyCommand
      DurationText = "00:00"
      IncrementDuration = WpfHelper.emptyCommand
      DecrementDuration = WpfHelper.emptyCommand
      PlayListFilePathText = ""
      SnapShotFolderPathText = ""
      Randomize = WpfHelper.emptyCommand
      DrawingCommand = WpfHelper.emptyCommand
      DrawingCommandText = "Start Drawing"
      State = RandomDrawingState.Stop
      CurrentDuration = ""
      CurrentFrames = 0
      Position = 0
      DrawingServiceVisibility = Visibility.Collapsed
      DrawingSettingVisibility = Visibility.Visible
      WindowClosed = WpfHelper.emptyCommand }

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
#if DEBUG
            .WriteTo
            .Console()
#endif
            .CreateLogger()

    let cmds =
        Interop.WindowInteropHelper(window).Handle
        |> toCmd

    WpfProgram.mkProgramWithCmdMsg init update bindings cmds
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.withSubscription
        (fun _ ->
            Cmd.batch [ Cmd.ofSub DrawingSetvice.setup
                        Cmd.ofSub PlayerLib.timeChanged
                        Cmd.ofSub PlayerLib.playerBuffering ])
    |> WpfProgram.startElmishLoop window
