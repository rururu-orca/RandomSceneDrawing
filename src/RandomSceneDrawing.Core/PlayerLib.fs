module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types
open FSharp.Control
open FSharpPlus
open System.Threading.Tasks

type AsyncBuilder with

    member x.Bind(t: Task<'T>, f: 'T -> Async<'R>) : Async<'R> = async.Bind(Async.AwaitTask t, f)
    member x.Bind(t: Task, f: unit -> Async<'R>) : Async<'R> = async.Bind(Async.AwaitTask t, f)

let initialize () = Core.Initialize()

let libVLC =
#if DEBUG
    new LibVLC("--verbose=2")
#else
    new LibVLC(false)
#endif

let initPlayer () =
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


let timeChanged (player: MediaPlayer) dispatch =
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

let playerBuffering (player: MediaPlayer) dispatch =
    player.Buffering
    |> AsyncSeq.ofObservableBuffered
    |> AsyncSeq.map (fun e -> e.Cache)
    |> AsyncSeq.iter (PlayerBuffering >> dispatch)
    |> Async.StartImmediate


let pickMediaState chooser (media: Media) =
    media.StateChanged
    |> AsyncSeq.ofObservableBuffered
    |> AsyncSeq.map (fun e -> e.State)
    |> AsyncSeq.pick chooser

let (|AlreadyBufferingCompleted|_|) (m, msg) =
    if m.RandomizeState = WaitBuffering
       && m.Player.State <> VLCState.Buffering then
        Some(m, msg)
    else
        None

let playAsync (player: MediaPlayer) onSuccess (media: Media) =
    async {
        let msg = $"{media.Mrl} の再生に失敗しました。"

        let result =
            media
            |> pickMediaState
                (function
                | VLCState.Playing as s -> Ok onSuccess |> Some
                | VLCState.Error -> Error(PlayFailedException msg) |> Some
                | _ -> None)

        if player.Play media then
            return! result
        else
            return Error(PlayFailedException msg)
    }

let pauseAsync (player: MediaPlayer) onSuccess =
    async {
        let result =
            player.Media
            |> pickMediaState
                (function
                | VLCState.Paused -> Some onSuccess
                | _ -> None)

        player.SetPause true
        return! result
    }

let resumeAsync (player: MediaPlayer) onSuccess =
    async {
        let result =
            player.Media
            |> pickMediaState
                (function
                | VLCState.Playing -> Some onSuccess
                | _ -> None)

        player.SetPause false
        return! result
    }

let togglePauseAsync (player: MediaPlayer) (onPlaying, onPaused) =
    async {
        let result =
            player.Media
            |> pickMediaState
                (function
                | VLCState.Playing -> Some onPlaying
                | VLCState.Paused -> Some onPaused
                | _ -> None)

        player.Pause()
        return! result
    }

let stopAsync (player: MediaPlayer) onSuccess =
    async {
        player.Stop()
        return onSuccess
    }

let randomize (player: MediaPlayer) (playListUri: Uri) =
    async {
        if player.IsPlaying then
            do! Async.Sleep 100 |> Async.Ignore
            do! stopAsync player ()

        let random = Random()

        let! playList = loadPlayList playListUri

        let media =
            playList.SubItems
            |> Seq.item (random.Next playList.SubItems.Count)

        do! media.Parse MediaParseOptions.ParseNetwork

        let rTime =
            random.Next(1000, int media.Duration - 3000)
            |> int64

        match!
            Async.StartChild(playAsync player () media, 1000)
            |> Async.join
            with
        | Ok _ ->
            player.Mute <- true
            player.SetAudioTrack -1 |> ignore

            do! pauseAsync player ()

            player.Time <- rTime
            do! resumeAsync player ()

            do! Async.Sleep 100 |> Async.Ignore

            if player.State = VLCState.Buffering then
                do!
                    player.Media.StateChanged
                    |> Async.AwaitEvent
                    |> Async.Ignore

            do! Async.Sleep 1500 |> Async.Ignore
            return RandomizeSuccess
        | Error ex -> return RandomizeFailed ex
    }

let getSize (player: MediaPlayer) num =
    let mutable px, py = 0u, 0u

    if player.Size(num, &px, &py) then
        Some(player, px, py)
    else
        None

let takeSnapshot sizefn num path =
    monad {
        let! (player: MediaPlayer), px, py = sizefn 0u

        if player.TakeSnapshot(num, path, px, py) then
            return! Some path
        else
            return! None
    }
