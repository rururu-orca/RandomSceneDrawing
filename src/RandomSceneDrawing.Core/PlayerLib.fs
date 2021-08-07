module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types

do Core.Initialize()

let libVLC =
#if DEBUG
    new LibVLC("--verbose=2")
#else
    new LibVLC(false)
#endif

let getMediaFromUri source = new Media(libVLC, uri = source)

let player = new MediaPlayer(libVLC)

let play source =
    async {
        match player.State with
        | VLCState.NothingSpecial
        | VLCState.Stopped
        | VLCState.Ended
        | VLCState.Error ->
            use media = getMediaFromUri source
            player.Play media |> ignore

            let! e = media.DurationChanged |> Async.AwaitEvent

            return
                PlaySuccess
                    { Title = media.Meta MetadataType.Title
                      Duration = float e.Duration |> TimeSpan.FromMilliseconds }

        | VLCState.Paused ->
            player.Pause() |> ignore

            return
                PlaySuccess
                    { Title = player.Media.Meta MetadataType.Title
                      Duration =
                          float player.Media.Duration
                          |> TimeSpan.FromMilliseconds }
        | VLCState.Opening
        | VLCState.Buffering
        | VLCState.Playing
        | _ -> return PlayFailed player.State
    }

let pause () =
    async {
        player.Pause()
        return PauseSuccess
    }

let stop () =
    async {
        player.Stop()

        player.Media.DurationChanged
        |> Async.AwaitEvent
        |> ignore

        return StopSuccess
    }
