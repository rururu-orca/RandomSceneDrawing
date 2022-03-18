module RandomSceneDrawing.Tests.Main


open Expecto
open Utils
open RandomSceneDrawing.Types
open RandomSceneDrawing.Main
open System

let apiOk = Api.mockOk ()
let apiError: Api<unit> = Api.mockError ()

let onExitMock =
    { new IObservable<int> with
        member _.Subscribe i =
            { new IDisposable with
                member _.Dispose() = () } }

let init = init () () onExitMock |> fst
let updatePlayer = update apiOk DrawingSettings.api

// Sub Model Test

let mainPlayerTest =
    Player.Core.msgTestSet
        "Model.MainPlayer"
        init
        (fun model state -> { model with MainPlayer = state })
        (fun msg -> PlayerMsg(MainPlayer, msg))
        updatePlayer

let subPlayerTest =
    Player.Core.msgTestSet
        "Model.SubPlayer"
        init
        (fun model state -> { model with SubPlayer = state })
        (fun msg -> PlayerMsg(SubPlayer, msg))
        updatePlayer

let updateSettings settingsApi =
    update apiOk settingsApi RandomSceneDrawing.Player.ApiMock.apiOk

let settingTest =
    DrawingSettings.msgTestSet
        "Model.Settings"
        init
        (fun model state -> { model with Settings = state })
        SettingsMsg
        updateSettings

// Main Test

let stateSetting = init

let update api =
    update api DrawingSettings.api RandomSceneDrawing.Player.ApiMock.apiOk


let expectNoChange initModel msg =
    Expect.elmishUpdate (update apiOk) "Should not be changed." initModel msg id initModel []

let expectWhenOkApi initModel msg expectModel expectMsgs =
    Expect.elmishUpdate (update apiOk) "Should be changed." initModel msg id expectModel expectMsgs

let testRandomize =
    testList
        "Main Model Randomize Cmd"
        [ testAsync "Randomize Started When Inprogress" {
              let expectModel = { stateSetting with RandomizeState = InProgress }
              do! expectNoChange expectModel [ Randomize Started ]
          }

          testAsync "Randomize Started" {
              let expectModel = { stateSetting with RandomizeState = InProgress }
              let expectMsg = [ (Ok >> Finished >> Randomize) () ]
              do! expectWhenOkApi stateSetting [ Randomize Started ] expectModel expectMsg
          }

          testAsync "Randomize Finished" {
              let stateInprogress = { stateSetting with RandomizeState = InProgress }
              let msg = [ (Ok >> Finished >> Randomize) () ]

              let expectModel = { stateInprogress with RandomizeState = Resolved(Ok()) }
              let expectMsg = []
              do! expectWhenOkApi stateInprogress msg expectModel expectMsg
          } ]


let testWhenSetting =
    testList
        "Main Model when Setting"
        [ testAsync "Tick" { do! expectNoChange stateSetting [ Tick ] }

          testAsync "StartDrawing Started" {
              let msg = [ StartDrawing Started ]

              let expectModel = Model.initInterval stateSetting

              let expectMsg = [ (Ok >> Finished >> StartDrawing) () ]
              do! expectWhenOkApi stateSetting msg expectModel expectMsg
          }

          testAsync "StartDrawing Finished Error" {
              let msg = [ (Error >> Finished >> StartDrawing) "Mock" ]

              let stateStarted = Model.initInterval stateSetting

              let expectModel = stateSetting

              let expectMsg = []
              do! expectWhenOkApi stateStarted msg expectModel expectMsg
          }
          testAsync "StartDrawing Finished Ok" {
              let msg = [ (Ok >> Finished >> StartDrawing) () ]

              let stateStarted = Model.initInterval stateSetting

              do! expectWhenOkApi stateStarted msg stateStarted [ Tick ]
          }

          testAsync "Stop Drawing" { do! expectNoChange stateSetting [ StopDrawing ] }

          ]

let stateInitInterval = Model.initInterval stateSetting

let toInterval state =
    (match state with
     | Interval i -> Some i
     | _ -> None)
    |> Expect.wantSome
    <| "Want Interval"

let toRunning state =
    (match state with
     | Running i -> Some i
     | _ -> None)
    |> Expect.wantSome
    <| "Want Running"

let stateInitRunning =
    { stateSetting with
        RandomizeState = Resolved(Ok())
        State =
            Running
                { Duration = stateSetting.Settings.Settings.Duration
                  Frames = ValueTypes.frames.Create 1 } }

open System

let testWhenRunning =
    testList
        "Main Model when Running"
        [ testAsync "StartDrawing" { do! expectNoChange stateInitRunning [ StartDrawing Started ] }

          testAsync "StopDrawing" {
              let msg = [ StopDrawing ]

              let expectModel = { stateInitRunning with State = Setting }
              let expectMsg = []
              do! expectWhenOkApi stateInitRunning msg expectModel expectMsg
          }

          testAsync "Tick when Ramdomize Error" {
              let msg = [ Tick ]

              let initState =
                  { stateInitRunning with RandomizeState = (Error >> Resolved) "Test" }

              let expectModel = { stateInitRunning with RandomizeState = InProgress }

              let expectMsg =
                  [ (Ok >> Finished >> Randomize) ()
                    Tick ]

              do! expectWhenOkApi initState msg expectModel expectMsg
          }

          testAsync "Tick CountDownDrawing" {

              let msg = [ Tick ]

              let expectModel =
                  let r = toRunning stateInitRunning.State

                  { r with Duration = ValueTypes.countDown ValueTypes.duration r.Duration }
                  |> stateInitRunning.WithState

              let expectMsg = [ Tick ]

              do! expectWhenOkApi stateInitRunning msg expectModel expectMsg
          }

          testAsync "Tick Continue" {

              let initModel =
                  let r = toRunning stateInitRunning.State

                  { r with Duration = ValueTypes.duration.Create TimeSpan.Zero }
                  |> stateInitRunning.WithState

              let msg = [ Tick ]

              let expectModel =
                  let r = toRunning initModel.State

                  DrawingRunning.toInterval r initModel
                  |> initModel.WithState

              let expectMsg = [ Tick ]

              do! expectWhenOkApi initModel msg expectModel expectMsg
          }

          testAsync "Tick AtLast" {

              let initModel =
                  let r = toRunning stateInitRunning.State
                  let frame = stateInitRunning.Settings.Settings.Frames

                  { r with
                      Duration = ValueTypes.duration.Create TimeSpan.Zero
                      Frames = frame }
                  |> stateInitRunning.WithState

              let msg = [ Tick ]

              let expectModel = { initModel with State = Setting }

              let expectMsg = []

              do! expectWhenOkApi initModel msg expectModel expectMsg
          }

          ]



let testWhenInterval =
    testList
        "Main Model when Interval"
        [ testAsync "StartDrawing" { do! expectNoChange stateInitInterval [ StartDrawing Started ] }
          testAsync "StopDrawing" {
              let msg = [ StopDrawing ]

              let expectModel = { stateInitInterval with State = Setting }
              let expectMsg = []
              do! expectWhenOkApi stateInitInterval msg expectModel expectMsg
          }

          testAsync "Tick At First" {

              let msg = [ Tick ]

              let expectModel =
                  { toInterval stateInitInterval.State with Init = InProgress }
                  |> { stateInitInterval with RandomizeState = InProgress }
                      .WithState

              let expectMsg =
                  [ (Ok >> Finished >> Randomize) ()
                    Tick ]

              do! expectWhenOkApi stateInitInterval msg expectModel expectMsg
          }
          testAsync "Tick when Ramdomize Error" {
              let msg = [ Tick ]

              let initState =
                  { stateInitInterval with RandomizeState = (Error >> Resolved) "Test" }

              let expectModel = { stateInitInterval with RandomizeState = InProgress }

              let expectMsg =
                  [ (Ok >> Finished >> Randomize) ()
                    Tick ]

              do! expectWhenOkApi initState msg expectModel expectMsg
          }

          testAsync "Tick CountDown" {
              let initState = stateInitInterval

              let msg =
                  [
                    // At First
                    Tick
                    // ignored
                    Tick
                    // Init Resolve
                    (Ok >> Finished >> Randomize) ()
                    // CountDown
                    Tick ]

              let expectModel =
                  let i = toInterval stateInitInterval.State

                  { i with
                      Init = Resolved()
                      Interval = ValueTypes.countDown ValueTypes.interval i.Interval }
                  |> { stateInitInterval with RandomizeState = (Ok >> Resolved) () }
                      .WithState

              let expectMsg =
                  [
                    // At First
                    (Ok >> Finished >> Randomize) ()
                    Tick
                    // Resolvad
                    Tick
                    // CountDown
                    Tick ]

              do! expectWhenOkApi initState msg expectModel expectMsg
          }

          testAsync "Tick Zero" {

              let initState =
                  let i = toInterval stateInitInterval.State

                  { i with
                      Init = Resolved()
                      Interval = ValueTypes.interval.Create TimeSpan.Zero }
                  |> { stateInitInterval with RandomizeState = (Ok >> Resolved) () }
                      .WithState

              let msg = [ Tick ]



              let expectMsg = [ Tick ]

              do! expectWhenOkApi initState msg stateInitRunning expectMsg
          } ]

[<Tests>]
let mainTest =
    testList
        "Main"
        [ // sub model
          mainPlayerTest
          subPlayerTest
          settingTest

          // main test
          testRandomize
          testWhenSetting
          testWhenRunning
          testWhenInterval ]
