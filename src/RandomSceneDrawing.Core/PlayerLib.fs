module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types
open FSharp.Control
open FSharpPlus
open RandomSceneDrawing.DrawingSetvice
open System.Threading.Tasks

type AsyncBuilder with
    member x.Bind(t: Task<'T>, f: 'T -> Async<'R>) : Async<'R> = async.Bind(Async.AwaitTask t, f)
    member x.Bind(t: Task, f: unit -> Async<'R>) : Async<'R> = async.Bind(Async.AwaitTask t, f)

do Core.Initialize()

let libVLC =
#if DEBUG
    new LibVLC("--verbose=2")
#else
    new LibVLC(false)
#endif


let player =
    new MediaPlayer(
        libVLC,
        FileCaching = 500u,
        NetworkCaching = 500u,
        EnableHardwareDecoding = true,
        Mute = true,
        Volume = 0
    )

let getMediaFromUri source = new Media(libVLC, uri = source)

let loadPlayList source =
    async {
        let playList = new Media(libVLC, uri = source)

        do! playList.Parse MediaParseOptions.ParseNetwork

        return playList
    }


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
    |> Async.StartImmediate

let playerBuffering dispatch =
    player.Buffering
    |> AsyncSeq.ofObservableBuffered
    |> AsyncSeq.map (fun e -> e.Cache)
    |> AsyncSeq.iter (PlayerBuffering >> dispatch)
    |> Async.StartImmediate

let play media =
    async {
        match player.State with
        | VLCState.NothingSpecial
        | VLCState.Stopped
        | VLCState.Ended
        | VLCState.Playing
        | VLCState.Error ->
            player.Play media |> ignore

            let! e = media.DurationChanged |> Async.AwaitEvent
            player.SetAudioTrack -1 |> ignore

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

exception PlayFailedException

let randomize (playListUri: Uri) dispatch =
    let cts =
        new Threading.CancellationTokenSource(TimeSpan.FromSeconds(5.0))

    Async.StartImmediate(

        async {
            use! cancel =
                Async.OnCancel
                <| fun () -> RandomizeFailed(TimeoutException()) |> dispatch

            let play media =
                let started = player.Playing |> Async.AwaitEvent

                if not (player.Play media) then
                    dispatch (RandomizeFailed PlayFailedException)

                Async.RunSynchronously(started, 1000)

            let pause () =
                let paused = player.Paused |> Async.AwaitEvent
                player.Pause()
                paused |> Async.RunSynchronously

            let stop () =
                if player.IsPlaying then
                    let stopped = player.Stopped |> Async.AwaitEvent
                    player.Stop()
                    stopped |> Async.RunSynchronously |> Some
                else
                    None

            stop () |> ignore


            player.Mute <- true
            player.SetAudioTrack -1 |> ignore

            let random = Random()
            let! playList = loadPlayList playListUri

            let media =
                playList.SubItems
                |> Seq.item (random.Next playList.SubItems.Count)

            do! media.Parse MediaParseOptions.ParseNetwork

            let rTime =
                random.Next(1000, int media.Duration - 3000)
                |> int64

            play media |> ignore

            pause () |> ignore

            player.Mute <- true
            player.SetAudioTrack -1 |> ignore
            player.Time <- rTime

            float player.Time
            |> TimeSpan.FromMilliseconds
            |> PlayerTimeChanged
            |> dispatch

            do! Async.Sleep 100 |> Async.Ignore

            float player.Time
            |> TimeSpan.FromMilliseconds
            |> PlayerTimeChanged
            |> dispatch

            do! Async.Sleep 100 |> Async.Ignore
            dispatch RandomizeSuccess
        },
        cts.Token
    )

let getSize num =
    let mutable px, py = 0u, 0u

    if player.Size(num, &px, &py) then
        Some(px, py)
    else
        None

let takeSnapshot sizefn num path =
    monad {
        let! px, py = sizefn 0u

        if player.TakeSnapshot(num, path, px, py) then
            return! Some path
        else
            return! None
    }
