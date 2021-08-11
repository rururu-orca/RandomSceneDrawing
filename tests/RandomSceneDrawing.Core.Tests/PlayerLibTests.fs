module RandomSceneDrawing.Tests.PlayerLib

open System
open Expecto
open RandomSceneDrawing.PlayerLib
open System.IO
open LibVLCSharp.Shared
open Expecto

[<Tests>]
let playerLibTests =
    testList "VlcLib"
    <| [ test "can get MediaPlayer instance" { Expect.isNotNull player "" }
         test "can get Media instance" {
             Expect.isNotNull
                 (Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4"
                  |> getMediaFromUri)
                 ""
         }

         testAsync "can load Playlist"  {
             let path =
                 [| __SOURCE_DIRECTORY__
                    "TestPlayList.xspf" |]
                 |> Path.Combine
             let! playList = loadPlayList (Uri path)

             Expect.equal playList.Type MediaType.File ""

         }
         test "can randomize"  {
             let path =
                 [| __SOURCE_DIRECTORY__
                    "TestPlayList.xspf" |]
                 |> Path.Combine
             let playList = loadPlayList (Uri path) |> Async.RunSynchronously
             do randomize playList |> ignore
             
             let media:Media = player.Media
             Expect.notEqual media.Duration -1L ""
             Expect.isGreaterThan player.Time 0L ""
         }

         ]
