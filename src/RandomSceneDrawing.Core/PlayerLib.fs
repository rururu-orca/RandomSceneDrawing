module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types
open FSharp.Control
open FSharpPlus

do Core.Initialize()

let libVLC =
#if DEBUG
    new LibVLC("--verbose=2")
#else
    new LibVLC(false)
#endif

let getMediaFromUri source = new Media(libVLC, uri = source)

let loadPlayList source =
    async {
        let playList = new Media(libVLC, uri = source)

        do!
            playList.Parse MediaParseOptions.ParseNetwork
            |> Async.AwaitTask
            |> Async.Ignore

        return playList
    }

let player = new MediaPlayer(libVLC)

let timeChanged dispatch =
    player.TimeChanged
    |> AsyncSeq.ofObservableBuffered
    |> AsyncSeq.map (fun e -> e.Time / 1000L)
    |> AsyncSeq.distinctUntilChanged
    |> AsyncSeq.iter (
        float
        >> TimeSpan.FromSeconds
        >> PlayerTimeChanged
        >> dispatch
    )
    |> Async.Start



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
        do! player.Paused |> Async.AwaitEvent |> Async.Ignore
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

let ramdomize (playList: Media) =
    player.Stop()
    let random = Random()

    let media =
        playList.SubItems
        |> Seq.item (random.Next playList.SubItems.Count)

    async {
        do!
            media.Parse MediaParseOptions.ParseNetwork
            |> Async.AwaitTask
            |> Async.Ignore
    }
    |> Async.RunSynchronously

    let rec waitPaused currentTime =
        Threading.Thread.Yield() |> ignore
        if currentTime <> player.Time then
            waitPaused player.Time

    let rTime =
        random.Next(100, int media.Duration) |> int64

    do player.Play media |> ignore
    while player.Time <= 0L do
        Threading.Thread.Yield() |> ignore


    player.Pause()
    waitPaused player.Time
    player.Time <- rTime

    // Play
    player.Pause()
    while player.Time = rTime do
        Threading.Thread.Yield() |> ignore

    player.Pause()
    waitPaused player.Time

    RandomizeSuccess
