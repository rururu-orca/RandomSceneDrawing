module RandomSceneDrawing.Tests.Core

open Expecto

open Elmish
open System
open System.IO

open RandomSceneDrawing
open RandomSceneDrawing.Types
open RandomSceneDrawing.Util
open RandomSceneDrawing.Program

let init =
    { Frames = config.Frames
      Duration = config.Duration
      Interval = config.Interval
      Player = PlayerLib.initPlayer ()
      PlayerMediaInfo = HasNotStartedYet
      SubPlayer = PlayerLib.initSubPlayer ()
      PlayerState = Stopped
      MediaDuration = TimeSpan.Zero
      MediaPosition = TimeSpan.Zero
      PlayerBufferCache = 0.0f
      RandomizeState = Waiting
      PlayListFilePath = config.PlayListFilePath
      SnapShotFolderPath = config.SnapShotFolderPath
      SnapShotPath = ""
      Title = ""
      RandomDrawingState = RandomDrawingState.Stop
      CurrentDuration = TimeSpan.Zero
      CurrentFrames = 0
      StatusMessage = "" }


let errorResult = Error "Not Implemented"
let errorFinished = Finished errorResult
let errorResolved = Resolved errorResult

let api =
    { playAsync = fun _ -> task { return Play errorFinished }
      pauseAsync = fun _ -> failwith "Not Implemented"
      stopAsync = fun _ -> failwith "Not Implemented"
      randomizeAsync = fun _ -> failwith "Not Implemented"
      createCurrentSnapShotFolderAsync = fun _ -> failwith "Not Implemented"
      takeSnapshotAsync = fun _ -> failwith "Not Implemented"
      startDrawing = fun _ -> failwith "Not Implemented"
      stopDrawingAsync = fun _ -> failwith "Not Implemented"
      selectPlayListFilePathAsync = fun _ -> failwith "Not Implemented"
      selectSnapShotFolderPathAsync = fun _ -> failwith "Not Implemented"
      showErrorAsync = fun _ -> failwith "Not Implemented" }

let initUpdate api = updateProto api

let foldMessages initialState msgs update =
    msgs
    |> List.fold (fun (state, _) message -> update message state) (initialState, [])


[<Tests>]
let tests =
    testList
        "Player Tests"
        [ testTask "Play Started" {
              let update = updateProto api

              let model', _ = update |> foldMessages init [ Play Started ]
              Expect.equal model'.PlayerMediaInfo InProgress "Should be changed."
          }
          testTask "Play Started InProgress" {
              let update = updateProto api

              let init = { init with PlayerMediaInfo = InProgress }
              
              let model', _ = update |> foldMessages init [ Play Started ]
              Expect.equal model' init "Should not be changed."
          }
          testTask "Play Finished" {
              let update = updateProto api

              let init = { init with PlayerMediaInfo = InProgress }
              
              let model', _ = update |> foldMessages init [ Play errorFinished ]
              Expect.equal model'.PlayerMediaInfo errorResolved "Should be resolved."
          }

          ]
