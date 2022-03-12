module RandomSceneDrawing.Tests.DrawingSettings

open System
open Expecto
open Expecto.Accuracy
open Utils

open RandomSceneDrawing.Types
open RandomSceneDrawing.Types.ErrorTypes
open RandomSceneDrawing.DrawingSettings
open RandomSceneDrawing.DrawingSettings.ValueTypes

open FsToolkit.ErrorHandling

let api =
    { pickPlayList = fun _ -> task { return Ok "Test" }
      pickSnapshotFolder = fun _ -> task { return Ok "Foo" } }

let update = update api

let updateValidatedValueTest testLabel msg msgMapper update valid invalid mapper =
    let state = Settings.Default() |> Model.create

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state msg msgMapper expectModel expectMsgs

    testList
        testLabel
        [ testAsync "Set Valid" {

              let expectState =
                  state.WithSettings(fun m -> mapper m valid)

              do! expectUpdate "should be changed" (msg valid) expectState []
          }
          testAsync "Set Invalid" {
              let expect =
                  state.WithSettings(fun m -> mapper m invalid)

              do! expectUpdate "should be changed." (msg invalid) expect []
          } ]

let testFileSystemPickerCommand testMessage msg mapper settingsMapper apiFunc =
    let expectUpdate testMessage init msg expectModel expectMsg =
        Expect.elmishUpdate update testMessage init msg id expectModel expectMsg

    let state = Settings.Default() |> Model.create

    testList
        testMessage
        [ testAsync "Started" {
              let expect = mapper state InProgress

              let! expectMsg =
                  apiFunc ()
                  |> Task.map (Finished >> msg)
                  |> Async.AwaitTask

              do! expectUpdate "should run PickPlayList cmd" state (msg Started) expect [ expectMsg ]
          }
          testAsync "Started when InProgress" {
              let state = mapper state InProgress
              do! expectUpdate "should be no change" state (msg Started) state []
          }
          testAsync "Finished when Ok" {
              let state = mapper state InProgress
              let returnValue = "test"
              let result = Ok returnValue
              let msg' = (Finished >> msg) result

              let expect =
                  fun m -> settingsMapper m returnValue
                  |> Model.withSettings (mapper state (Resolved result))


              do! expectUpdate "should be change" state msg' expect []
          }
          testAsync "Finished when Error" {
              let state = mapper state InProgress
              let result = Error Canceled
              let msg' = (Finished >> msg) result

              let expect = (mapper state (Resolved result))

              do! expectUpdate "should be change" state msg' expect []
          }

          ]

let msgTestSet label model modelMapper msgMapper update =
    let updateValidatedValueTest testLabel msg valid invalid mapper =
        updateValidatedValueTest testLabel msg msgMapper valid invalid mapper

    testList
        label
        [

        ]

[<Tests>]
let tests =
    let updateValidatedValueTest testLabel msg valid invalid mapper =
        updateValidatedValueTest testLabel msg id update valid invalid mapper

    testList
        "Drawing Model"
        [ updateValidatedValueTest "Model Frames" SetFrames 1 -1 (fun settings newValue ->
              { settings with Frames = settings.Frames |> frames.Update newValue })

          updateValidatedValueTest "Model Duration" SetDuration TimeSpan.Zero (TimeSpan -1) (fun settings newValue ->
              { settings with Duration = settings.Duration |> duration.Update newValue })

          updateValidatedValueTest "Model Interval" SetInterval TimeSpan.Zero (TimeSpan -1) (fun settings newValue ->
              { settings with Interval = settings.Interval |> interval.Update newValue })

          updateValidatedValueTest "Model PlayListFilePath" SetPlayListFilePath "" "-1" (fun settings newValue ->
              { settings with
                  PlayListFilePath =
                      settings.PlayListFilePath
                      |> playListFilePath.Update newValue })

          testFileSystemPickerCommand
              "PickPlayList"
              PickPlayList
              (fun model newValue -> { model with PickedPlayListPath = newValue })
              (fun settings newValue ->
                  { settings with
                      PlayListFilePath =
                          settings.PlayListFilePath
                          |> playListFilePath.Update newValue })
              api.pickPlayList

          updateValidatedValueTest "Model SnapShotFolderPath" SetSnapShotFolderPath "" "-1" (fun settings newValue ->
              { settings with
                  SnapShotFolderPath =
                      settings.SnapShotFolderPath
                      |> snapShotFolderPath.Update newValue })

          testFileSystemPickerCommand
              "PickSnapshotFolder"
              PickSnapshotFolder
              (fun model newValue -> { model with PickedSnapShotFolderPath = newValue })
              (fun settings newValue ->
                  { settings with
                      SnapShotFolderPath =
                          settings.SnapShotFolderPath
                          |> snapShotFolderPath.Update newValue })
              api.pickSnapshotFolder ]
