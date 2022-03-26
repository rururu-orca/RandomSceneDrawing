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

let api = RandomSceneDrawing.DrawingSettings.ApiMock.api
let initRandomizeInfo = initRandomizeInfoDomain api.validateMediaInfo

let testUpdateValidatedValue testLabel model modelMapper msg msgMapper update valid invalid mapper =
    let sttings = init api
    let state = modelMapper model sttings

    let expectUpdate testMessage msg expectModel expectMsgs =
        Expect.elmishUpdate update testMessage state [ msg ] msgMapper expectModel expectMsgs

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

let testValidate: ValidatedValueTestFunc<'Dto, Model, Msg, Api, 'ParentModel, 'ParentMsg> =
    fun parentModel modelMapper msgMapper update label msgLabel valid invalid dtoMapper ->
        let model = init api
        let state = modelMapper parentModel model

        let expectUpdate testMessage msgs expectModel expectMsgs =
            let update = update api
            Expect.elmishUpdate update testMessage state msgs msgMapper expectModel expectMsgs

        testList
            label
            [ testAsync "Set Valid" {
                  let expectModel = dtoMapper model valid |> modelMapper parentModel

                  do! expectUpdate "Should be Vaild" [ msgLabel valid ] expectModel []
              }
              testAsync "Set Invalid" {
                  let expectModel = dtoMapper model invalid |> modelMapper parentModel

                  do! expectUpdate "Should be Vaild" [ msgLabel invalid ] expectModel []
              } ]


let testFileSystemPickerCommand testMessage model modelMapper msg msgMapper mapper update settingsMapper apiFunc =
    let expectUpdate testMessage init msg expectModel expectMsg =
        Expect.elmishUpdate update testMessage init msg msgMapper expectModel expectMsg

    let sttings = init api

    let state = modelMapper model sttings

    testList
        testMessage
        [ testAsync "Started" {
              let expect = mapper sttings InProgress |> modelMapper model

              let! expectMsg =
                  apiFunc ()
                  |> Task.map (Finished >> msg >> msgMapper)
                  |> Async.AwaitTask

              do! expectUpdate "should run PickPlayList cmd" state [ msg Started ] expect [ expectMsg ]
          }
          testAsync "Started when InProgress" {
              let state = mapper sttings InProgress |> modelMapper model
              do! expectUpdate "should be no change" state [ msg Started ] state []
          }
          testAsync "Finished when Ok" {
              let settings = mapper sttings InProgress
              let state = modelMapper model settings
              let returnValue = "test"
              let result = Ok returnValue
              let msg' = msg (Finished result)

              let expect =
                  fun m -> settingsMapper m returnValue
                  |> Model.withSettings (mapper sttings (Resolved result))
                  |> modelMapper model

              do! expectUpdate "should be change" state [ msg' ] expect []
          }
          testAsync "Finished when Error" {
              let settings = mapper sttings InProgress
              let state = modelMapper model settings
              let result = Error Canceled
              let msg' = (Finished >> msg) result

              let expect =
                  (mapper settings (Resolved result))
                  |> modelMapper model

              do! expectUpdate "should be change" state [ msg' ] expect []
          } ]

let testSet: MsgTestSetFunc<Model, Msg, Api, 'ParentModel, 'ParentMsg> =
    fun label parentModel modelMapper msgMapper update ->
        let tempFilePath = IO.Path.GetTempFileName()
        let tempFolderPath = IO.Path.GetTempPath()

        use _ =
            { new IDisposable with
                member _.Dispose() = IO.File.Delete tempFilePath }


        let testValidate label msgLabel valid invalid dtoMapper =
            let testValidate' = testValidate parentModel modelMapper msgMapper update
            let dtoMapper' (m: Model) (dto: 'Dto) = dtoMapper dto |> m.WithSettings

            testValidate' label msgLabel valid invalid dtoMapper'

        let testFileSystemPickerCmd label msgLabel apiFunc dtoMapper settingsMapper =
            testFileSystemPickerCommand
                label
                parentModel
                modelMapper
                msgLabel
                msgMapper
                dtoMapper
                (update api)
                settingsMapper
                apiFunc

        testList
            label
            [ testValidate "Model Frames" SetFrames 1 -1 (fun dto settings ->
                  { settings with Frames = settings.Frames |> frames.Update dto })

              testValidate "Model Duration" SetDuration TimeSpan.Zero (TimeSpan -1) (fun dto settings ->
                  { settings with Duration = settings.Duration |> duration.Update dto })


              testValidate "Model Interval" SetInterval TimeSpan.Zero (TimeSpan -1) (fun dto settings ->
                  { settings with Interval = settings.Interval |> interval.Update dto })

              testValidate "Model PlayListFilePath" SetPlayListFilePath tempFilePath "-1" (fun dto settings ->
                  { settings with
                      PlayListFilePath =
                          settings.PlayListFilePath
                          |> playListFilePath.Update dto })

              testFileSystemPickerCmd
                  "PickPlayList"
                  PickPlayList
                  api.pickPlayList
                  (fun model newValue -> { model with PickedPlayListPath = newValue })
                  (fun settings newValue ->
                      { settings with
                          PlayListFilePath =
                              settings.PlayListFilePath
                              |> playListFilePath.Update newValue })

              testValidate "Model SnapShotFolderPath" SetSnapShotFolderPath tempFolderPath "-1" (fun dto settings ->
                  { settings with
                      SnapShotFolderPath =
                          settings.SnapShotFolderPath
                          |> snapShotFolderPath.Update dto })

              testFileSystemPickerCmd
                  "PickSnapshotFolder"
                  PickSnapshotFolder
                  api.pickSnapshotFolder
                  (fun model newValue -> { model with PickedSnapShotFolderPath = newValue })
                  (fun settings newValue ->
                      { settings with
                          SnapShotFolderPath =
                              settings.SnapShotFolderPath
                              |> snapShotFolderPath.Update newValue })

              let currentDefaultSettingModel () = init api

              let resetModel () =

                  Settings.reset initRandomizeInfo
                  |> (fun s ->
                      Settings.save initRandomizeInfo s
                      s)
                  |> Model.create

              let isEqualSetting a b =
                  let eq actual expect = Expect.equal actual expect ""

                  eq a.Frames b.Frames
                  eq a.Duration b.Duration
                  eq a.Interval b.Interval
                  eq a.PlayListFilePath b.PlayListFilePath
                  eq a.SnapShotFolderPath b.SnapShotFolderPath


                  (a.RandomizeInfoList, b.RandomizeInfoList)
                  ||> List.iter2 (fun ia ib ->
                      let ia = initRandomizeInfo.Dto ia
                      let ib = initRandomizeInfo.Dto ib
                      eq ia.MediaInfo.Duration ib.MediaInfo.Duration
                      eq ia.MediaInfo.Title ib.MediaInfo.Title
                      eq ia.Path ib.Path

                      (ia.TrimDurations, ib.TrimDurations)
                      ||> Seq.iter2 (fun ta tb ->
                          eq ta.Start tb.Start
                          eq ta.End tb.End))


              testAsync "Save and Reset Settings" {

                  let defaultSetting = resetModel ()

                  let init = modelMapper parentModel defaultSetting

                  let msg = [ SetFrames 10; SaveSettings ]

                  let expectSettings =
                      defaultSetting.WithSettings(fun s -> { s with Frames = frames.Create 10 })

                  let expectModel = expectSettings |> modelMapper parentModel

                  do! Expect.elmishUpdate (update api) "Model no Changed." init msg msgMapper expectModel []
                  let actualSetting = Settings.Default initRandomizeInfo

                  isEqualSetting actualSetting expectSettings.Settings

                  let resetSetting = resetModel ()

                  Expect.notEqual resetSetting expectSettings "Should be Not Equal"
              } ]

[<Tests>]
let settingTest =
    testSet "DrawingSettings" (init api) (fun _ s -> s) id (fun api -> (Cmds >> update) api)
