module RandomSceneDrawing.Tests.PlayerLib

open System
open Expecto
open RandomSceneDrawing
open System.IO
open LibVLCSharp.Shared
open Expecto

[<Tests>]
let playerLibTests =
    testList "VlcLib"
    <| [ test "can get MediaPlayer instance" { Expect.isNotNull PlayerLib.player "" }
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

             let! playList = PlayerLib.loadPlayList (Uri path)

             Expect.equal playList.Type MediaType.File ""

         }
         testAsync "can randomize" {
             let path =
                 [| __SOURCE_DIRECTORY__
                    "TestPlayList.xspf" |]
                 |> Path.Combine

             PlayerLib.randomize (Uri path) ignore

             do!
                 async {
                     do!
                         PlayerLib.player.TimeChanged
                         |> Async.AwaitEvent
                         |> Async.Ignore
                 }

             let media: Media = PlayerLib.player.Media
             Expect.notEqual media.Duration -1L ""
             Expect.isGreaterThan PlayerLib.player.Time 0L ""
         }

         testAsync "can take Snapshot" {
             let path =
                 Path.Combine [| __SOURCE_DIRECTORY__
                                 "TestPlayList.xspf" |]

             let snapShot =
                 Path.Combine [| __SOURCE_DIRECTORY__
                                 "test.png" |]


             do!
                 async {
                     PlayerLib.randomize (Uri path) ignore

                     do!
                         PlayerLib.player.TimeChanged
                         |> Async.AwaitEvent
                         |> Async.Ignore

                     Expect.isSome
                     <| PlayerLib.takeSnapshot PlayerLib.getSize 0u snapShot
                     <| ""                    
                 }

         }

         ]
