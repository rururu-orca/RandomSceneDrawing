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

let testUpdateValidatedValue testLabel model modelMapper msg msgMapper update valid invalid mapper =
    let sttings = Settings.Default() |> Model.create
    let state = modelMapper model sttings

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state msg msgMapper expectModel expectMsgs

    testList
        testLabel
        [ testAsync "Set Valid" {

              let expectState =
                  sttings.WithSettings(fun m -> mapper m valid)
                  |> modelMapper model

              do! expectUpdate "should be changed" (msg valid) expectState []
          }
          testAsync "Set Invalid" {
              let expect =
                  sttings.WithSettings(fun m -> mapper m invalid)
                  |> modelMapper model


              do! expectUpdate "should be changed." (msg invalid) expect []
          } ]

let testFileSystemPickerCommand testMessage model modelMapper msg msgMapper mapper update settingsMapper apiFunc =
    let expectUpdate testMessage init msg expectModel expectMsg =
        Expect.elmishUpdate update testMessage init msg msgMapper expectModel expectMsg

    let sttings = Settings.Default() |> Model.create

    let state = modelMapper model sttings

    testList
        testMessage
        [ testAsync "Started" {
              let expect = mapper sttings InProgress |> modelMapper model

              let! expectMsg =
                  apiFunc ()
                  |> Task.map (Finished >> msg >> msgMapper)
                  |> Async.AwaitTask

              do! expectUpdate "should run PickPlayList cmd" state ((msg) Started) expect [ expectMsg ]
          }
          testAsync "Started when InProgress" {
                let state = mapper sttings InProgress |> modelMapper model
                do! expectUpdate "should be no change" state (msg Started) state []
            }
          testAsync "Finished when Ok" {
                let settings = mapper sttings InProgress 
                let state =  modelMapper model settings
                let returnValue = "test"
                let result = Ok returnValue
                let msg' = msg (Finished result)

                let expect =
                    fun m -> settingsMapper m returnValue
                    |> Model.withSettings (mapper sttings (Resolved result) )
                    |> modelMapper model

                do! expectUpdate "should be change" state msg' expect []
            }
          testAsync "Finished when Error" {
                let settings = mapper sttings InProgress 
                let state =  modelMapper model settings
                let result = Error Canceled
                let msg' = (Finished >> msg) result

                let expect = (mapper settings (Resolved result)) |> modelMapper model

                do! expectUpdate "should be change" state msg' expect []
            }
          ]

let msgTestSet label model modelMapper msgMapper update =
    let update = update api

    let updateValidatedValueTest testLabel msg valid invalid mapper =
        testUpdateValidatedValue testLabel model modelMapper msg msgMapper update valid invalid mapper

    testList
        label
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
              model
              modelMapper
              PickPlayList
              msgMapper
              (fun model newValue -> { model with PickedPlayListPath = newValue })
              update
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
                model
                modelMapper
                PickSnapshotFolder
                msgMapper
                (fun model newValue -> { model with PickedSnapShotFolderPath = newValue })
                update
                (fun settings newValue ->
                    { settings with
                        SnapShotFolderPath =
                            settings.SnapShotFolderPath
                            |> snapShotFolderPath.Update newValue })
                api.pickSnapshotFolder
          ]

[<Tests>]
let settingTest = msgTestSet "DrawingSettings" () (fun _ s -> s) id update