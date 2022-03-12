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
    let stopped = Settings.Default() |> DrawingStopped.create
    let state = Stopped stopped

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state msg id expectModel expectMsgs

    testList
        testLabel
        [ testAsync "Set Valid" {

              let expect =
                  stopped.WithSettings(fun m -> mapper m valid)
                  |> Stopped

              do! expectUpdate "should be changed" (msg valid) expect []
          }
          testAsync "Set Invalid" {
              let expect =
                  stopped.WithSettings(fun m -> mapper m invalid)
                  |> Stopped

              do! expectUpdate "should be changed." (msg invalid) expect []
          } ]

let updateFilePathValueTest testLabel msg valid invalid mapper api =
    let settings = Settings.Default()
    let state = (DrawingStopped.create >> Stopped) settings

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

let testFileSystemPickerCommand testMessage msg mapper =
    let expectUpdate testMessage init msg expectModel expectMsg =
        Expect.elmishUpdate update testMessage init msg mapper expectModel expectMsg

    let settings = Settings.Default()
    let state = (DrawingStopped.create >> Stopped) settings

    testList
        testMessage
        [ ptestAsync "Started" { do! expectUpdate "First" state (msg Started) state [] }
          ptestAsync "Started when ..." { do! expectUpdate "First" state (msg Started) state [] } ]

[<FTests>]
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

          testFileSystemPickerCommand $"{PickPlayList}" PickPlayList id

          updateValidatedValueTest "Model SnapShotFolderPath" SetSnapShotFolderPath "" "-1" (fun settings newValue ->
              { settings with
                  SnapShotFolderPath =
                      settings.SnapShotFolderPath
                      |> snapShotFolderPath.Update newValue }) ]
