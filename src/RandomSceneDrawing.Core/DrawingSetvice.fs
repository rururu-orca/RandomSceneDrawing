module RandomSceneDrawing.DrawingSetvice

open System
open System.Threading
open FSharp.Control
open Elmish
open System.Windows
open Types

type TimeMeasure =
    | Seconds
    | Minutes
    | Hours

let timespan value measure =
    match measure with
    | Seconds -> TimeSpan.FromSeconds(value)
    | Minutes -> TimeSpan.FromMinutes(value)
    | Hours -> TimeSpan.FromHours(value)

let private timer = new System.Timers.Timer(1000.)

let setup dispatch =
    timer.Elapsed.Add(fun _ -> dispatch Tick)

let tickSub msg =
    timer.Start()
    msg

let stop () =
    timer.Stop()
    StopDrawingSuccess

module CountDownTimer =
    type Model =
        { TimerOn: bool
          Count: TimeSpan
          Period: TimeSpan }

    type Msg =
        | TimedTick
        | TimerToggled of bool

    type CmdMsg = TimerTick of TimeSpan

    let init count period =
        { TimerOn = false
          Count = count
          Period = period },
        []

    let update msg model =
        match msg with
        | TimerToggled on -> { model with TimerOn = on }, [ if on then yield TimerTick model.Period ]
        | TimedTick ->
            match model with
            | { TimerOn = false } -> model, []
            | { Count = ct; Period = stp } when ct > stp -> { model with Count = ct - stp }, [ TimerTick model.Period ]
            | _ ->
                { model with
                      TimerOn = false
                      Count = TimeSpan.Zero },
                []

    let timerCmd (period: TimeSpan) =
        async {
            do! Async.Sleep period
            return TimedTick
        }
        |> Cmd.OfAsync.result



[<Measure>]
type sec

[<Measure>]
type msec


module TimeSpan =
    let ofSec (sec: float<sec>) = TimeSpan.FromSeconds(float sec)
    let ofMsec (sec: float<msec>) = TimeSpan.FromMilliseconds(float sec)

    let addSec (sec: float<sec>) (ts: TimeSpan) = ts + TimeSpan.FromSeconds(float sec)

    let addMsec (sec: float<msec>) (ts: TimeSpan) =
        ts + TimeSpan.FromMilliseconds(float sec)

let sec s = (<|) TimeSpan.ofSec s

type Async with
    static member Sleep(sec: float<sec>) = TimeSpan.ofSec sec |> Async.Sleep

type Model =
    { Frames: int
      Duration: TimeSpan
      Interval: int
      Token: CancellationToken }

type CmdMessage =
    | Request
    | Success
    | Cancel
    | Failed of exn

type Msg =
    | TickSec of float<sec>
    | StartCountDown of CmdMessage


let startCountDown (tick: float<sec>) =
    async {
        do! Async.Sleep tick
        printfn "Starting sleep workflow at %O" DateTime.Now.TimeOfDay
        do! Async.Sleep tick
        printfn "Finished sleep workflow at %O" DateTime.Now.TimeOfDay
        return Success
    }

let update msg m =
    match msg with
    | TickSec sec ->
        { m with
              Duration = m.Duration |> TimeSpan.addSec -sec }
    | StartCountDown Request ->
        Cmd.OfAsync.either startCountDown 1.<sec> id Failed
        m
