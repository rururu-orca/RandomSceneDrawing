module RandomSceneDrawing.Tests.Player.Lib

open System
open Expecto
open FsToolkit.ErrorHandling
open LibVLCSharp.Shared
open RandomSceneDrawing
open RandomSceneDrawing.Player
open RandomSceneDrawing.Main.ValueTypes
open RandomSceneDrawing.PlayerLib
open System.IO


let playerApi media =
    { playAsync = fun player -> LibVLCSharp.playAsync player media
      pauseAsync = LibVLCSharp.pauseAsync
      stopAsync = LibVLCSharp.stopAsync
      showInfomation = fun _ -> task { () } }

let settingsApi: DrawingSettings.Api =
    { validateMediaInfo = RandomizeInfoDto.validate
      parsePlayListFile = RandomizeInfoDto.parsePlayListFile
      pickPlayList = fun _ -> task { return Ok "Test" }
      pickSnapshotFolder = fun _ -> task { return Ok "Foo" }
      showInfomation = fun _ -> task { () } }

let mainMock: Main.Api<MediaPlayer> = Main.Api.mockOk ()

let mainApi: Main.Api<MediaPlayer> =
    { step = mainMock.step
      createSnapShotFolder = fun _ -> task { return Ok "test" }
      copySubVideo = fun _ -> task { return Ok() }
      showInfomation = fun _ -> task { () }
      randomize = fun randomizeSource (player) (subPlayer) ->
        taskResult {
            let! resultInfo = Randomize.initSourceAsync randomizeSource player subPlayer
            do! Randomize.startSublayerAsync subPlayer

            do! Randomize.startMainPlayerAsync player

            do! LibVLCSharp.seekAsync resultInfo.Position player
            return resultInfo
        }
      takeSnapshot = LibVLCSharp.takeSnapshot }

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
    <| [ test "can get MediaPlayer instance" { Expect.isNotNull (LibVLCSharp.initPlayer ()) "" }
         test "can get Media instance" { Expect.isNotNull (LibVLCSharp.Media.ofUri mediaUrl) "" }
         testTask "can load Playlist" {
             let getMediaType (media: Media) = media.Type

             let! result =
                 (playListFilePath.Dto >> Uri) playListPath
                 |> LibVLCSharp.Media.ofUri
                 |> LibVLCSharp.Media.parseAsync MediaParseOptions.ParseNetwork
                 |> TaskResult.map getMediaType

             let actual = Expect.wantOk result "should work"

             Expect.equal MediaType.Playlist actual "should work"

         }
         testTask "can randomize and snapshot" {

             use player = LibVLCSharp.initPlayer ()
             use subPlayer = LibVLCSharp.initPlayer ()
             let randomizeSource = (playListFilePath.Value >> PlayList) playListPath

             let! randomizeResult = mainApi.randomize randomizeSource player subPlayer
             Expect.isOk randomizeResult "Randomize should Ok"

             let! takeSnapshotResult = LibVLCSharp.takeSnapshot player snapShot

             Expect.isOk takeSnapshotResult "takeSnapshot should Ok"

         } ]
    |> testSequencedGroup "LibTest"
