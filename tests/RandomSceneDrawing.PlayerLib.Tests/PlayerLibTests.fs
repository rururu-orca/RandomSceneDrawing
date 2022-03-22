module RandomSceneDrawing.Tests.Player.Lib

open System
open Expecto
open FsToolkit.ErrorHandling
open RandomSceneDrawing
open RandomSceneDrawing.Player
open RandomSceneDrawing.Main.ValueTypes
open System.IO
open LibVLCSharp

do PlayerLib.initialize ()

let playerApi media =
    { playAsync = fun player -> PlayerLib.playAsync player media
      pauseAsync = PlayerLib.pauseAsync
      stopAsync = PlayerLib.stopAsync
      showInfomation = fun _ -> task { () } }

let mainMock: Main.Api<MediaPlayer> = Main.Api.mockOk ()

let mainApi: Main.Api<MediaPlayer> =
    { step = mainMock.step
      createSnapShotFolder = fun _ -> task { return Ok "test" }
      copySubVideo = fun _ -> task { return Ok() }
      showInfomation = fun _ -> task { () }
      randomize = PlayerLib.Randomize.run
      takeSnapshot = PlayerLib.takeSnapshot }

[<Tests>]
let playerLibTests =


    let mediaUrl =
        Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"

    let playListPath =
        [| __SOURCE_DIRECTORY__
           "TestPlayList.xspf" |]
        |> Path.Combine
        |> playListFilePath.Create

    let snapShot =
        Path.Combine [|
            __SOURCE_DIRECTORY__
            "test.png"
        |]

    testList "VlcLib"
    <| [ test "can get MediaPlayer instance" { Expect.isNotNull (PlayerLib.initPlayer ()) "" }
         test "can get Media instance" { Expect.isNotNull (PlayerLib.getMediaFromUri mediaUrl) "" }
         testAsync "can load Playlist" {
             let! playList =
                 (playListFilePath.Dto >> Uri) playListPath
                 |> PlayerLib.loadPlayList
                 |> Async.AwaitTask

             Expect.equal playList.Type MediaType.Playlist "should work"

         }
         testTask "can randomize and snapshot" {

             use player = PlayerLib.initPlayer ()
             use subPlayer = PlayerLib.initPlayer ()
             let randomizeSource = (playListFilePath.Value >> PlayList) playListPath

             let! randomizeResult = PlayerLib.Randomize.run randomizeSource player subPlayer
             Expect.isOk randomizeResult "Randomize should Ok"

             let! takeSnapshotResult = PlayerLib.takeSnapshot player snapShot

             Expect.isOk takeSnapshotResult "takeSnapshot should Ok"

         } ]
    |> testSequencedGroup "LibTest"
