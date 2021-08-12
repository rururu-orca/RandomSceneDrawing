module RandomSceneDrawing.Program

open System
open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open Types
open RandomSceneDrawing

let init () =
    { Frames = 1
      Duration = TimeSpan(0, 0, 10)
      Interval = TimeSpan(0, 0, 5)
      DrawingServiceVisibility = Visibility.Collapsed
      Player = PlayerLib.player
      PlayerState = PlayerState.Stopped
      MediaDuration = TimeSpan.Zero
      MediaPosition = TimeSpan.Zero
      Title = ""
      RandomDrawingState = RandomDrawingState.Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0 },
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

    // Random Drawing
    | RequestRandomize (_) -> { m with PlayerState = Randomizung }, [ Randomize ]
    | RandomizeSuccess (_) ->
        { m with
              Title = m.Player.Media.Meta LibVLCSharp.Shared.MetadataType.Title
              PlayerState = Playing
              MediaDuration = (float m.Player.Length |> TimeSpan.FromMilliseconds) },
        [ ]
    | RandomizeFailed (_) -> { m with PlayerState = Stopped },[Stop;Randomize]
    | RequestStartDrawing (_) -> m, [ StartDrawing ]
    | RequestStopDrawing (_) -> m, [ StopDrawing ]
    | StartDrawingSuccess (_) ->
        { m with
              CurrentFrames = 1
              CurrentDuration = m.Interval
              RandomDrawingState = Interval
              PlayerState = Randomizung },
        [Randomize]
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
            [Randomize]
        else
            { m with
                  CurrentDuration = TimeSpan.Zero },
            [ StopDrawing ]



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
              | {RandomDrawingState = Interval}
              | {PlayerState = Randomizung}
              | {PlayerState = Stopped} -> Visibility.Collapsed
              | {PlayerState = Playing}
              | {PlayerState = Paused} -> Visibility.Visible)

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

      // Random Drawing
      "Randomize" |> Binding.cmdIf (RequestRandomize,(fun m -> m.PlayerState <> Randomizung))
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
              | RandomDrawingState.Stop -> "Start Drawing"
              | Running
              | Interval -> "Stop Drawing")


      "DrawingServiceVisibility"
      |> Binding.oneWay
          (fun m ->
              match m.RandomDrawingState with
              | RandomDrawingState.Stop -> Visibility.Collapsed
              | Running
              | Interval -> Visibility.Visible) ]



let toCmd =
    function
    // Player
    | Play ->
        Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
        |> PlayerLib.play
        |> Cmd.OfAsync.result
    | Pause -> Cmd.OfAsync.either PlayerLib.pause () id PauseFailed
    | Stop -> Cmd.OfAsync.either PlayerLib.stop () id StopFailed
    // Random Drawing
    | Randomize ->
        Cmd.ofSub (PlayerLib.randomize (Uri @"C:\repos\RandomSceneDrawing\tools\PlayList.xspf"))
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
      Randomize = WpfHelper.emptyCommand
      DrawingCommand = WpfHelper.emptyCommand
      DrawingCommandText = "Start Drawing"
      State = RandomDrawingState.Stop
      CurrentDuration = ""
      CurrentFrames = 0
      Position = 0
      DrawingServiceVisibility = Visibility.Collapsed }

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


    WpfProgram.mkProgramWithCmdMsg init update bindings toCmd
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.withSubscription
        (fun _ ->
            Cmd.batch [ Cmd.ofSub DrawingSetvice.setup
                        Cmd.ofSub PlayerLib.timeChanged ])
    |> WpfProgram.startElmishLoop window
