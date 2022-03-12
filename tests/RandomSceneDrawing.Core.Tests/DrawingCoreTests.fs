module RandomSceneDrawing.Tests.DrawingCore

open System
open Expecto
open Expecto.Accuracy
open Utils

open RandomSceneDrawing.Types
open RandomSceneDrawing.Types.ErrorTypes
open RandomSceneDrawing.Drawing
open RandomSceneDrawing.Drawing.ValueTypes

open FsToolkit
open FsToolkit.ErrorHandling

let api =
    { step = fun _ -> async { do! Async.Sleep 2 }
      pickPlayList = fun _ -> task { return Ok "Test" }
      pickSnapshotFolder = fun _ -> task { return Ok "Foo" } }

let update = update api

let updateValidatedValueTest testLabel msg valid invalid mapper =
    let stopped = Settings.Default() |> DrawingStopped.create
    let state = Stopped stopped

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state msg id expectModel expectMsgs

    testList
        testLabel
        [ testAsync "Set Valid" {

              let expectState =
                  stopped.WithSettings(fun m -> mapper m valid)
                  |> Stopped

              do! expectUpdate "should be changed" (msg valid) expectState []
          }
          testAsync "Set Invalid" {
              let expect =
                  stopped.WithSettings(fun m -> mapper m invalid)
                  |> Stopped

              do! expectUpdate "should be changed." (msg invalid) expect []
          } ]

let testFileSystemPickerCommand testMessage msg mapper settingsMapper apiFunc =
    let expectUpdate testMessage init msg expectModel expectMsg =
        Expect.elmishUpdate update testMessage init msg id expectModel expectMsg

    let stopped = Settings.Default() |> DrawingStopped.create
    let state = Stopped stopped

    testList
        testMessage
        [ testAsync "Started" {
              let expect = mapper stopped InProgress |> Stopped

              let! expectMsg =
                  apiFunc ()
                  |> Task.map (Finished >> msg)
                  |> Async.AwaitTask

              do! expectUpdate "should run PickPlayList cmd" state (msg Started) expect [ expectMsg ]
          }
          testAsync "Started when InProgress" {
              let state = mapper stopped InProgress |> Stopped
              do! expectUpdate "should be no change" state (msg Started) state []
          }
          testAsync "Finished when Ok" {
              let stopped = mapper stopped InProgress
              let state = Stopped stopped
              let returnValue = "test"
              let result = Ok returnValue
              let msg' = (Finished >> msg) result

              let expect =
                  fun m -> settingsMapper m returnValue
                  |> DrawingStopped.withSettings (mapper stopped (Resolved result))
                  |> Stopped


              do! expectUpdate "should be change" state msg' expect []
          }
          testAsync "Finished when Error" {
              let stopped = mapper stopped InProgress
              let state = Stopped stopped
              let result = Error Canceled
              let msg' = (Finished >> msg) result

              let expect = (mapper stopped (Resolved result)) |> Stopped

              do! expectUpdate "should be change" state msg' expect []
          }

          ]

[<Tests>]
let tests =
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
              (fun stopped newValue -> { stopped with PickedPlayListPath = newValue })
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
              (fun stopped newValue -> { stopped with PickedSnapShotFolderPath = newValue })
              (fun settings newValue ->
                  { settings with
                      SnapShotFolderPath =
                          settings.SnapShotFolderPath
                          |> snapShotFolderPath.Update newValue })
              api.pickSnapshotFolder ]
