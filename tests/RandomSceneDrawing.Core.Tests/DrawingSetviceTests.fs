module RandomSceneDrawing.Tests

open System
open Elmish
open Expecto
open RandomSceneDrawing.DrawingSetvice
open System.Threading
open System.Windows

[<Tests>]
let timerModelTests =
    let createTimerModel timerOn count step : CountDownTimer.Model =
        { TimerOn = timerOn
          Step = TimeSpan.ofSec count
          Count = TimeSpan.ofSec step }

    testList
        "Timer"
        [ test "Toggling On Should Trigger Timer Tick " {

              let expectedReturn =
                  createTimerModel true 1.0<sec> 0.0<sec>, [ CountDownTimer.TimerTick ]

              let acturalReturn =
                  createTimerModel false 1.0<sec> 0.0<sec>
                  |> CountDownTimer.update (CountDownTimer.TimerToggled true)

              "Toggling On Should TimerOn"
              |> Expect.equal acturalReturn expectedReturn
          }

          test "On timeup" {
              let expectedReturn =
                  createTimerModel false 1.0<sec> 0.0<sec>, [ ]

              let acturalReturn =
                  createTimerModel true 1.0<sec> 1.0<sec>
                  |> CountDownTimer.update (CountDownTimer.TimedTick)

              "Toggling Off Should TimerOn"
              |> Expect.equal acturalReturn expectedReturn
          }

         ]


[<Tests>]
let tests =


    let initialState =
        { Frames = 1
          Duration = TimeSpan.ofSec 30.0<sec>
          Interval = 1
          Token = CancellationToken.None }

    let foldMessages state messages =
        messages
        |> List.fold (fun state message -> update message state) state

    testList
        "DrawingSetvice"
        [ test "A simple countdown" {

            let atctual =
                [ TickSec 1.0<sec> ] |> foldMessages initialState


            "Model can countdown from Message"
            |> Expect.equal atctual.Duration (TimeSpan.ofSec 29.0<sec>)
          }

          testAsync "countdown" {
              let atctual =
                  [ TickSec 1.0<sec> ]
                  |> foldMessages
                      { initialState with
                            Duration = (TimeSpan.ofSec 5.0<sec>) }

              ignore

          } ]
