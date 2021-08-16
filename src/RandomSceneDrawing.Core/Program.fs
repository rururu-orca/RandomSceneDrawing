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
      PlayListFilePath = config.PlayListFilePath
      SnapShotFolderPath = config.SnapShotFolderPath
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
    | PlaySuccess mediaInfo ->
        { m with
              Title = mediaInfo.Title
              PlayerState = Playing
              MediaDuration = mediaInfo.Duration },
        []
    | PlayFailed _ -> failwith "Not Implemented"
    | RequestPause -> m, [ Pause ]
    | PauseSuccess -> m, []
    | PauseFailed _ -> failwith "Not Implemented"
    | RequestStop -> m, [ Stop ]
    | StopSuccess -> { m with PlayerState = Stopped }, []
    | StopFailed _ -> failwith "Not Implemented"
    | PlayerTimeChanged time -> { m with MediaPosition = time }, []

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
    | RequestRandomize (_) -> { m with PlayerState = Randomizung }, [ Randomize ]
    | RandomizeSuccess (_) ->
        { m with
              Title = m.Player.Media.Meta LibVLCSharp.Shared.MetadataType.Title
              PlayerState = Playing
              MediaDuration = (float m.Player.Length |> TimeSpan.FromMilliseconds) },
        []
    | RandomizeFailed (_) -> { m with PlayerState = Stopped }, [ Stop; Randomize ]
    | RequestStartDrawing (_) -> m, [ StartDrawing ]
    | RequestStopDrawing (_) -> m, [ StopDrawing ]
    | StartDrawingSuccess (_) ->
        { m with
              CurrentFrames = 1
              CurrentDuration = m.Interval
              RandomDrawingState = Interval
              PlayerState = Randomizung },
        [ Randomize ]
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
                  RandomDrawingState = Running
                  CurrentDuration = m.Duration },
            []
        elif m.CurrentFrames < m.Frames then
            { m with
                  RandomDrawingState = Interval
                  PlayerState = Randomizung
                  CurrentFrames = m.CurrentFrames + 1
                  CurrentDuration = m.Interval },
            [ Randomize ]
        else
            { m with
                  CurrentDuration = TimeSpan.Zero },
            [ StopDrawing ]

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
              | { PlayerState = Randomizung }
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
      |> Binding.cmdIf (RequestRandomize, (fun m -> m.PlayerState <> Randomizung))
      "CurrentDuration"
      |> Binding.oneWay (fun m -> m.CurrentDuration)
      "CurrentFrames"
      |> Binding.oneWay (fun m -> m.CurrentFrames)

      "DrawingCommand"
      |> Binding.cmd
          (fun (m: Model) ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> RequestStartDrawing
              | Running
              | Interval -> RequestStopDrawing)
      "DrawingCommandText"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> "⏲ Start Drawing"
              | Running
              | Interval -> "Stop Drawing")
      "DrawingSettingVisibility"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> Visibility.Visible
              | Running
              | Interval -> Visibility.Collapsed)

      "DrawingServiceVisibility"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> Visibility.Collapsed
              | Running
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


module DialogHelper =
    open Windows.Foundation
    open Windows.Storage.Pickers
    open WinRT.Interop

    type AsyncBuilder with
        member x.Bind(t: IAsyncOperation<'T>, f: 'T -> Async<'R>) : Async<'R> =
            async.Bind(t.AsTask() |> Async.AwaitTask, f)


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

let toCmd hwnd =
    function
    // Player
    | Play ->
        Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
        |> PlayerLib.play
        |> Cmd.OfAsync.result
    | Pause -> Cmd.OfAsync.either PlayerLib.pause () id PauseFailed
    | Stop -> Cmd.OfAsync.either PlayerLib.stop () id StopFailed

    | SelectPlayListFilePath -> Cmd.OfAsync.either DialogHelper.selectPlayList hwnd id SelectPlayListFilePathFailed
    | SelectSnapShotFolderPath ->
        Cmd.OfAsync.either DialogHelper.selectSnapShotFolder hwnd id SelectSnapShotFolderPathFailed
    // Random Drawing
    | Randomize -> Cmd.ofSub (PlayerLib.randomize (Uri @"C:\repos\RandomSceneDrawing\tools\PlayList.xspf"))
    | StartDrawing -> Cmd.OfFunc.either DrawingSetvice.tickSub StartDrawingSuccess id StartDrawingFailed
    | StopDrawing -> Cmd.OfFunc.result <| DrawingSetvice.stop ()

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
                        Cmd.ofSub PlayerLib.timeChanged ])
    |> WpfProgram.startElmishLoop window
