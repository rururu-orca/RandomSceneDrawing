module RandomSceneDrawing.Program

open System
open System.Windows
open Serilog
open Serilog.Extensions.Logging
open Elmish.WPF
open LibVLCSharp.Shared
open LibVLCSharp.WPF

type State =
    | Stop
    | Running

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: int
      DrawingServiceVisibility: Visibility
      Libvlc: LibVLC
      Player: MediaPlayer
      VideoSeze: Size
      MediaDuration: TimeSpan
      MediaPosition: float
      Title: string
      State: State
      CurrentDuration: TimeSpan
      CurrentFrames: int }

let init () =
    Core.Initialize()
#if DEBUG
    let libvlc = new LibVLC("--verbose=2")
#else
    let libvlc = new LibVLC(false)
#endif
    { Frames = 0
      Duration = TimeSpan.Zero
      Interval = 0
      DrawingServiceVisibility = Visibility.Collapsed
      Libvlc = libvlc
      Player = new MediaPlayer(libvlc)
      VideoSeze = Size()
      MediaDuration = TimeSpan.Zero
      MediaPosition = 0.0
      Title = ""
      State = Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0 }

type Msg =
    | Play
    | Pause
    | Stop
    | Randomize
    | SetDuration of TimeSpan
    | SetFrames of int
    | VideoViewLoaded of VideoView


let update (msg: Msg) (model: Model) : Model =
    match msg with
    | Play ->
        match model.Player.State with
        | VLCState.NothingSpecial
        | VLCState.Stopped
        | VLCState.Ended
        | VLCState.Error ->
            model.Libvlc
            |> PlayerLib.getMediaFromUri
                "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
            |> model.Player.Play
            |> ignore
        | VLCState.Paused -> model.Player.Pause()
        | VLCState.Opening
        | VLCState.Buffering
        | VLCState.Playing
        | _ -> ()

        { model with
              MediaDuration =
                  float model.Player.Media.Duration
                  |> TimeSpan.FromMilliseconds }
    | Pause ->
        model.Player.Pause()
        model
    | Stop ->
        model.Player.Stop()
        model
    | Randomize -> model
    | SetDuration x -> { model with Duration = x }
    | SetFrames x -> { model with Frames = x }
    | VideoViewLoaded v ->
        v.MediaPlayer <- model.Player
        model

let paramToVideoView (p: obj) =
    let args = p :?> RoutedEventArgs
    args.Source :?> VideoView |> VideoViewLoaded

let bindings () : Binding<Model, Msg> list =
    [ "Pause" |> Binding.cmd Pause
      "Play" |> Binding.cmd Play
      "Stop" |> Binding.cmd Stop
      "Randomize" |> Binding.cmd Randomize
      "CurrentDuration"
      |> Binding.oneWay (fun m -> m.CurrentDuration)
      "CurrentFrames"
      |> Binding.oneWay (fun m -> m.CurrentFrames)
      "Duration"
      |> Binding.twoWay ((fun m -> m.Duration.ToString @"mm\:ss"), (TimeSpan.Parse >> SetDuration))
      "Frames"
      |> Binding.twoWay ((fun m -> string m.Frames), (int >> SetFrames))
      "Position"
      |> Binding.oneWay (fun m -> m.Player.Time)
      "ScenePosition"
      |> Binding.oneWay (fun m -> m.MediaDuration)
      "SourceDuration"
      |> Binding.oneWay (fun m -> m.MediaDuration)
      "SourceName" |> Binding.oneWay (fun m -> m.Title)
      "DrawingServiceVisibility"
      |> Binding.oneWay (fun m -> m.DrawingServiceVisibility)
      "VideoViewLoaded"
      |> Binding.cmdParam paramToVideoView ]

let designVm =
    ViewModel.designInstance (init ()) (bindings ())

let main window =
    let logger =
        LoggerConfiguration()
            .MinimumLevel.Override("Elmish.WPF.Update", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Bindings", Events.LogEventLevel.Verbose)
            .MinimumLevel.Override("Elmish.WPF.Performance", Events.LogEventLevel.Verbose)
            .WriteTo.Console()
            .CreateLogger()

    WpfProgram.mkSimple init update bindings
    |> WpfProgram.withLogger (new SerilogLoggerFactory(logger))
    |> WpfProgram.startElmishLoop window
