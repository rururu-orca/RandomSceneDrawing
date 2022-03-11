module RandomSceneDrawing.Tests.PlayerCore

open Expecto

open System

open RandomSceneDrawing.Types
open Utils

module Player =
    open RandomSceneDrawing.Player

    let stateStopped = init ()

    let errorResult = Error "Not Implemented"
    let errorFinished = Finished errorResult
    let errorResolved = Resolved errorResult

    let statePlayInProgress =
        { stateStopped with
            Media = InProgress
            State = Started }

    let statePlayError =
        { stateStopped with
            Media = errorResolved
            State = errorFinished }

    let okMediaInfo = Ok { Title = ""; Duration = TimeSpan.Zero }

    let stateMediaPlaying =
        { stateStopped with
            Media = Resolved okMediaInfo
            State = Finished(Ok Playing) }

    let statePauseInProgress = { stateMediaPlaying with State = Started }

    let stateMediaPaused = { statePauseInProgress with State = Finished(Ok Paused) }

    let statePauseError = { statePauseInProgress with State = errorFinished }

    let stateStopInProgress = statePauseInProgress

    let stateStopError = statePauseError

    let apiOk =
        { playAsync = fun _ -> task { return Play(Finished okMediaInfo) }
          pauseAsync = fun _ -> task { return Pause(Finished okMediaInfo) }
          stopAsync = fun _ -> task { return Stop(Finished(Ok())) } }

    let apiError =
        { playAsync = fun _ -> task { return Play errorFinished }
          pauseAsync = fun _ -> task { return Pause errorFinished }
          stopAsync = fun _ -> task { return Stop errorFinished } }

    let msgTestSet label model modelMapper msgMapper update =

        let expectModelOkApi initModel msg expectedUpdatedModel =
            Expect.model
                (update apiOk)
                "Should be changed."
                (modelMapper model initModel)
                msg
                msgMapper
                (modelMapper model expectedUpdatedModel)

        let expectModelErrorApi initModel msg expectedUpdatedModel =
            Expect.model
                (update apiError)
                "Should be changed."
                (modelMapper model initModel)
                msg
                msgMapper
                (modelMapper model expectedUpdatedModel)

        let expectModelNoChange initModel msg =
            Expect.model
                (update apiOk)
                "Should not be changed."
                (modelMapper model initModel)
                msg
                msgMapper
                (modelMapper model initModel)

        testList
            label
            [ test "Play Started when Media InProgress" { expectModelNoChange statePlayInProgress (Play Started) }

              test "Play Started when Media" { expectModelOkApi stateStopped (Play Started) statePlayInProgress }
              test "Play Finished Ok" {
                  expectModelOkApi stateStopped ((Finished >> Play) okMediaInfo) stateMediaPlaying
              }
              test "Play Finished Error" { expectModelErrorApi stateStopped (Play errorFinished) statePlayError }
              test "Pause Started when Media HasNotStartedYet" { expectModelNoChange stateStopped (Pause Started) }
              test "Pause Started when Media InProgress" { expectModelNoChange statePlayInProgress (Pause Started) }
              test "Pause Started when Media errorResolved" { expectModelNoChange statePlayError (Pause Started) }
              test "Pause Started" { expectModelOkApi stateMediaPlaying (Pause Started) statePauseInProgress }
              test "Pause Finished Ok" {
                  expectModelOkApi statePauseInProgress ((Finished >> Pause) okMediaInfo) stateMediaPaused
              }
              test "Pause Finished Error" {
                  expectModelErrorApi statePauseInProgress (Pause errorFinished) statePauseError
              }
              test "Stop Started when Media HasNotStartedYet" { expectModelNoChange stateStopped (Stop Started) }
              test "Stop Started when Media InProgress" { expectModelNoChange statePlayInProgress (Stop Started) }
              test "Stop Started when Media errorResolved" { expectModelNoChange statePlayError (Stop Started) }
              test "Stop Started" { expectModelOkApi stateMediaPlaying (Stop Started) stateStopInProgress }
              test "Stop Finished Ok" {
                  expectModelOkApi stateStopInProgress ((Ok >> Finished >> Stop) ()) stateStopped
              }
              test "Stop Finished Error" { expectModelErrorApi stateStopInProgress (Stop errorFinished) stateStopError }

              ]

    [<Tests>]
    let testSet =
        msgTestSet "Player Model Base" (init ()) (fun _ state -> state) id update

module Main =
    open RandomSceneDrawing.Main
    let init = init () ()

    [<Tests>]
    let mainPlayerTest =
        Player.msgTestSet
            "Model.MainPlayer"
            init
            (fun model state -> { model with MainPlayer = state })
            (fun msg -> PlayerMsg(MainPlayer, msg))
            update

    [<Tests>]
    let subPlayerTest =
        Player.msgTestSet
            "Model.SubPlayer"
            init
            (fun model state -> { model with SubPlayer = state })
            (fun msg -> PlayerMsg(SubPlayer, msg))
            update
