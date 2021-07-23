module RandomSceneDrawing.Program

open Elmish.WPF
open LibVLCSharp.Shared
open System

type State =
    | Stop
    | Running

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: int
      Libvlc: LibVLC
      Player: MediaPlayer
      MediaDuration: TimeSpan
      MediaPosition: float 
      Title: string
      State: State
      CurrentDuration: TimeSpan
      CurrentFrames: int }

let init () =
    let libvlc = new LibVLC(true)
    { Frames = 0
      Duration = TimeSpan.Zero
      Interval = 0
      Libvlc = libvlc
      Player = new MediaPlayer(libvlc)
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

let update (msg: Msg) (model: Model) : Model =
    match msg with
    | Play ->
        use media = PlayerLib.getMediaFromlocal "file://nasne-df3531/share1/VIDEO/%E3%83%95%E3%82%A9%E3%83%88%E3%82%B9%E3%82%BF%E3%82%B8%E3%82%AA/Screen_Recording_20210604-125335.mp4" model.Libvlc
        model.Player.Play media |> ignore
        {model with MediaDuration = float media.Duration |> TimeSpan.FromMilliseconds}
    | Pause ->
        model.Player.Pause()
        model
    | Stop ->
        model.Player.Stop()
        model
    | Randomize -> model
    | SetDuration x -> { model with Duration = x }
    | SetFrames x -> { model with Frames = x }

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
      |> Binding.twoWay ((fun m -> string m.Duration), (TimeSpan.Parse >> SetDuration))
      "Frames"
      |> Binding.twoWay ((fun m -> float m.Frames), (int >> SetFrames))
      "Position" |> Binding.oneWay (fun m -> m.Player.Time)
      "ScenePosition" |> Binding.oneWay (fun m -> m.MediaDuration)
      "SourceDuration" |> Binding.oneWay (fun m -> m.MediaDuration)
      "SourceName" |> Binding.oneWay (fun m -> m.Title)
      // "DrawingServiceVisibility" |> Binding.
      "MediaPlayer" |> Binding.oneWay (fun m -> m.Player)
      ]

let main window =
    WpfProgram.mkSimple init update bindings
    |> WpfProgram.startElmishLoop window
