module RandomSceneDrawing.DrawingSetvice

open System

[<Measure>]
type sec

[<Measure>]
type msec

module TimeSpan =
    let ofSec (sec: float<sec>) = TimeSpan.FromSeconds(float sec)
    let ofMsec (sec: float<msec>) = TimeSpan.FromMilliseconds(float sec)
    let addSec (sec: float<sec>) (ts: TimeSpan) =
        ts + TimeSpan.FromSeconds(float sec)
    let addMsec (sec: float<msec>) (ts: TimeSpan) =
        ts + TimeSpan.FromMilliseconds(float sec)

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: int }

type Msg = TickSec of float<sec>

let update msg m =
    match msg with
    | TickSec sec ->
        { m with
              Duration = m.Duration |> TimeSpan.addSec -sec }