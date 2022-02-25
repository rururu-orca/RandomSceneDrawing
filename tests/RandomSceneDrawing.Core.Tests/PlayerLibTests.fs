module RandomSceneDrawing.Tests.PlayerLib

open System
open Expecto
open RandomSceneDrawing
open System.IO
open LibVLCSharp

[<Tests>]
let playerLibTests =
    do PlayerLib.initialize ()

    testList "VlcLib"
    <| [ test "can get MediaPlayer instance" { Expect.isNotNull (PlayerLib.initPlayer ()) "" }
         test "can get Media instance" {
             Expect.isNotNull
                 (Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
                  |> PlayerLib.getMediaFromUri)
                 ""
         }

         testAsync "can load Playlist" {
             let path =
                 [| __SOURCE_DIRECTORY__
                    "TestPlayList.xspf" |]
                 |> Path.Combine

             let! playList = PlayerLib.loadPlayList (Uri path) |> Async.AwaitTask

             Expect.equal playList.Type MediaType.File ""

         }
         testAsync "can randomize" {
             let path =
                 [| __SOURCE_DIRECTORY__
                    "TestPlayList.xspf" |]
                 |> Path.Combine

             use player = PlayerLib.initPlayer ()
             use subPlayer = PlayerLib.initPlayer ()

             do!
                 PlayerLib.randomize player subPlayer (Uri path)
                 |> Async.AwaitTask
                 |> Async.Ignore

             let media: Media = player.Media
             Expect.notEqual media.Duration -1L ""
             Expect.isGreaterThan player.Time 0L ""
         }

         testAsync "can take Snapshot" {
             let path =
                 Path.Combine [|
                     __SOURCE_DIRECTORY__
                     "TestPlayList.xspf"
                 |]

             let snapShot =
                 Path.Combine [|
                     __SOURCE_DIRECTORY__
                     "test.png"
                 |]

             use player = PlayerLib.initPlayer ()
             use subPlayer = PlayerLib.initPlayer ()

             do!
                 PlayerLib.randomize player subPlayer (Uri path)
                 |> Async.AwaitTask
                 |> Async.Ignore

             Expect.isSome
             <| PlayerLib.takeSnapshot (PlayerLib.getSize player) 0u snapShot
             <| ""

         }

         ]
