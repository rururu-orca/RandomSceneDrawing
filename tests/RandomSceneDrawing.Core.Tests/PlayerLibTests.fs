module RandomSceneDrawing.Tests.Player.Lib

open System
open Expecto
open RandomSceneDrawing
open RandomSceneDrawing.Types
open System.IO
open LibVLCSharp

[<Tests>]
let playerLibTests =
    do PlayerLib.initialize ()

    let mediaUrl =
        Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"

    let playListPath =
        [| __SOURCE_DIRECTORY__
           "TestPlayList.xspf" |]
        |> Path.Combine

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
                 PlayerLib.loadPlayList (Uri playListPath)
                 |> Async.AwaitTask

             Expect.equal playList.Type MediaType.Playlist "should work"

         }
         testTask "can randomize and snapshot" {

             use player = PlayerLib.initPlayer ()
             use subPlayer = PlayerLib.initPlayer ()

             let! actuel = PlayerLib.randomize player subPlayer (Uri playListPath)

             let media: Media = player.Media
             Expect.notEqual media.Duration -1L ""
             Expect.equal actuel RandomizeSuccess ""

             let actual = PlayerLib.takeSnapshot (PlayerLib.getSize player) 0u snapShot
             Expect.isSome actual "should Some"
         } ]
    |> testSequencedGroup "LibTest"
