module RandomSceneDrawing.Tests.Player.Core

open Expecto

open System

open RandomSceneDrawing.Types
open RandomSceneDrawing.Tests.Utils

open RandomSceneDrawing.Player
open RandomSceneDrawing.Player.ApiMock

let stateStopped = init ()

let statePlayInProgress =
    { stateStopped with
        Media = InProgress
        State = Started }

let statePlayError =
    { stateStopped with
        Media = errorResolved
        State = errorFinished }

let stateMediaPlaying =
    { stateStopped with
        Media = Resolved okMediaInfo
        State = Finished(Ok Playing) }

let statePauseInProgress = { stateMediaPlaying with State = Started }

let stateMediaPaused = { statePauseInProgress with State = Finished(Ok Paused) }

let statePauseError = { statePauseInProgress with State = errorFinished }

let stateStopInProgress = statePauseInProgress

let stateStopError = statePauseError

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
          test "Play Finished Ok" { expectModelOkApi stateStopped ((Finished >> Play) okMediaInfo) stateMediaPlaying }
          test "Play Finished Error" { expectModelErrorApi stateStopped (Play errorFinished) statePlayError }
          test "Pause Started when Media HasNotStartedYet" { expectModelNoChange stateStopped (Pause Started) }
          test "Pause Started when Media InProgress" { expectModelNoChange statePlayInProgress (Pause Started) }
          test "Pause Started when Media errorResolved" { expectModelNoChange statePlayError (Pause Started) }
          test "Pause Started" { expectModelOkApi stateMediaPlaying (Pause Started) statePauseInProgress }
          test "Pause Finished Ok" {
              expectModelOkApi statePauseInProgress ((Finished >> Pause) okMediaInfo) stateMediaPaused
          }
          test "Pause Finished Error" { expectModelErrorApi statePauseInProgress (Pause errorFinished) statePauseError }
          test "Stop Started when Media HasNotStartedYet" { expectModelNoChange stateStopped (Stop Started) }
          test "Stop Started when Media InProgress" { expectModelNoChange statePlayInProgress (Stop Started) }
          test "Stop Started when Media errorResolved" { expectModelNoChange statePlayError (Stop Started) }
          test "Stop Started" { expectModelOkApi stateMediaPlaying (Stop Started) stateStopInProgress }
          test "Stop Finished Ok" { expectModelOkApi stateStopInProgress ((Ok >> Finished >> Stop) ()) stateStopped }
          test "Stop Finished Error" { expectModelErrorApi stateStopInProgress (Stop errorFinished) stateStopError }

          ]

[<Tests>]
let testSet =
    msgTestSet "Player Model Base" (init ()) (fun _ state -> state) id update
