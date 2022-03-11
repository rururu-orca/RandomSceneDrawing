module RandomSceneDrawing.Tests.DrawingCore

open System
open Expecto
open Expecto.Accuracy
open Utils

open RandomSceneDrawing.Types
open RandomSceneDrawing.Drawing
open RandomSceneDrawing.Drawing.ValueTypes

open FsToolkit
open FsToolkit.ErrorHandling

let api =
    { step = fun _ -> async { do! Async.Sleep 2 }
      pickPlayList = fun _ -> task { return "Test" }
      pickSnapshotFolder = fun _ -> task { return "Foo" } }

let update = update api

let updateValidatedValueTest testLabel msg valid invalid mapper =
    let settings = Settings.Default()
    let state = Stopped settings

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state msg id expectModel expectMsgs

    testList
        testLabel
        [ testAsync "Set Valid" {
              let expect = mapper settings valid

              do! expectUpdate "should be changed" (msg valid) expect []
          }
          testAsync "Set Invalid" {
              let expect = mapper settings invalid
              do! expectUpdate "should be changed." (msg invalid) expect []
          } ]

let updateFilePathValueTest testLabel msg valid invalid mapper api =
    let settings = Settings.Default()
    let state = Stopped settings

    testList
        testLabel
        [ updateValidatedValueTest testLabel msg valid invalid mapper

          testTask "Set None" {
              let! ret = api ()
              let expectMsg = msg ret

              do!
                  Expect.elmishUpdate update "should be changed." state (PickPlayList Started) id state [ expectMsg ]
                  |> Async.StartAsTask
          } ]

[<Tests>]
let tests =
    testList
        "Drawing Model"
        [ updateValidatedValueTest "Model Frames" SetFrames 1 -1 (fun settings newValue ->
              Stopped { settings with Frames = settings.Frames |> frames.Update newValue })

          updateValidatedValueTest "Model Duration" SetDuration TimeSpan.Zero (TimeSpan -1) (fun settings newValue ->
              Stopped { settings with Duration = settings.Duration |> duration.Update newValue })

          updateValidatedValueTest "Model Interval" SetInterval TimeSpan.Zero (TimeSpan -1) (fun settings newValue ->
              Stopped { settings with Interval = settings.Interval |> interval.Update newValue })

          updateValidatedValueTest "Model PlayListFilePath" SetPlayListFilePath "" "-1" (fun settings newValue ->
              Stopped
                  { settings with
                      PlayListFilePath =
                          settings.PlayListFilePath
                          |> playListFilePath.Update newValue })

          updateValidatedValueTest "Model SnapShotFolderPath" SetSnapShotFolderPath "" "-1" (fun settings newValue ->
              Stopped
                  { settings with
                      SnapShotFolderPath =
                          settings.SnapShotFolderPath
                          |> snapShotFolderPath.Update newValue }) ]
