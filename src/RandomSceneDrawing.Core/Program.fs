﻿module RandomSceneDrawing.Program

open System
open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish
open Elmish.WPF
open Types
open RandomSceneDrawing

let init () =
    { Frames = 0
      Duration = TimeSpan.Zero
      Interval = 0
      DrawingServiceVisibility = Visibility.Collapsed
      Player = PlayerLib.player
      MediaDuration = TimeSpan.Zero
      MediaPosition = 0.0
      Title = ""
      State = State.Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0 },
    []

let update msg m =
    match msg with
    // Player
    | RequestPlay -> m, [ Play ]
    | PlaySuccess mediaInfo ->
        { m with
              Title = mediaInfo.Title
              MediaDuration = mediaInfo.Duration },
        []
    | PlayFailed _ -> failwith "Not Implemented"
    | RequestPause -> m, [ Pause ]
    | PauseSuccess -> m, []
    | PauseFailed _ -> failwith "Not Implemented"
    | RequestStop -> m, [ Stop ]
    | StopSuccess -> m, []
    | StopFailed _ -> failwith "Not Implemented"

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
    | RequestRandomize (_) -> failwith "Not Implemented"
    | RandomizeSuccess (_) -> failwith "Not Implemented"
    | RandomizeFailed (_) -> failwith "Not Implemented"



let bindings () : Binding<Model, Msg> list =
    [
      // Player
      "MediaPlayer"
      |> Binding.oneWay (fun m -> m.Player)
      "ScenePosition"
      |> Binding.oneWay (fun m -> m.MediaPosition)
      "SourceDuration"
      |> Binding.oneWay (fun m -> m.MediaDuration)
      "SourceName" |> Binding.oneWay (fun m -> m.Title)

      "Pause" |> Binding.cmd RequestPause
      "Play" |> Binding.cmd RequestPlay
      "Stop" |> Binding.cmd RequestStop

      // Random Drawing Setting
      "FramesText"
      |> Binding.twoWay ((fun m -> string m.Frames), (int >> SetFrames))
      "IncrementFrames" |> Binding.cmd IncrementFrames
      "DecrementFrames" |> Binding.cmd DecrementFrames

      "DurationText"
      |> Binding.twoWay ((fun m -> m.Duration.ToString @"mm\:ss"), (TimeSpan.Parse >> SetDuration))
      "IncrementDuration"
      |> Binding.cmd IncrementDuration
      "DecrementDuration"
      |> Binding.cmd DecrementDuration

      // Random Drawing
      "Randomize" |> Binding.cmd RequestRandomize
      "CurrentDuration"
      |> Binding.oneWay (fun m -> m.CurrentDuration)
      "CurrentFrames"
      |> Binding.oneWay (fun m -> m.CurrentFrames)
      "Position"
      |> Binding.oneWay (fun m -> m.Player.Time)

      "DrawingServiceVisibility"
      |> Binding.oneWay (fun m -> m.DrawingServiceVisibility)

      ]

let toCmd =
    function
    // Player
    | Play ->
        PlayerLib.play "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
        |> Cmd.OfAsync.result
    | Pause -> Cmd.OfAsync.either PlayerLib.pause () id PauseFailed
    | Stop -> Cmd.OfAsync.either PlayerLib.stop () id StopFailed
    // Random Drawing
    | Randomize -> failwith "Not Implemented"
    | StartDrawing -> failwith "Not Implemented"

let designVm =
    {
        MediaPlayer = PlayerLib.player
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
        CurrentDuration = ""
        CurrentFrames = 0
        Position = 0
        DrawingServiceVisibility = Visibility.Collapsed
    }

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
#if DEBUG
            .WriteTo.Console()
#endif
            .CreateLogger()


    WpfProgram.mkProgramWithCmdMsg init update bindings toCmd
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
