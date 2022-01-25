module RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types
open FSharp.Control
open FSharpPlus
open System.Threading.Tasks

[<AutoOpen>]
module Helper =
    let toSecf mSec = float mSec / 1000.0

let settings =
    {| randomize =
        {| min = 1000
           maxOffset = 3000
           SleepTime = {| onPlay = 100; onComplated = 1500 |} |}
       repeat =
        {| offset = 1250L
           time = 1000
           rate = 0.5f |}
       playFailedMsg = fun mediaInfo -> $"{mediaInfo} の再生に失敗しました。" |}


let initialize () = Core.Initialize()

let libVLC =
#if DEBUG
    new LibVLC("--verbose=2", "--no-snapshot-preview", "--no-audio")
#else
    new LibVLC("--no-snapshot-preview", "--no-audio")
#endif

let initPlayer () =
    new MediaPlayer(libVLC, FileCaching = 500u, NetworkCaching = 500u, EnableHardwareDecoding = true)

let initSubPlayer () =
    new MediaPlayer(libVLC, FileCaching = 500u, NetworkCaching = 500u, EnableHardwareDecoding = true)
    |> tap (fun p -> p.SetRate settings.repeat.rate |> ignore)


let getMediaFromUri source = new Media(libVLC, uri = source)

let loadPlayList source =
    task {
        let playList = new Media(libVLC, uri = source)

        let! _ = playList.Parse MediaParseOptions.ParseNetwork

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
        let msg = settings.playFailedMsg media.Mrl

        let result =
            media
            |> pickMediaState (function
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
            |> pickMediaState (function
                | VLCState.Paused -> Some onSuccess
                | _ -> None)

        player.SetPause true
        return! result
    }

let resumeAsync (player: MediaPlayer) onSuccess =
    async {
        let result =
            player.Media
            |> pickMediaState (function
                | VLCState.Playing -> Some onSuccess
                | _ -> None)

        player.SetPause false
        return! result
    }

let togglePauseAsync (player: MediaPlayer) (onPlaying, onPaused) =
    async {
        let result =
            player.Media
            |> pickMediaState (function
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

let subscribeThumbnailPlayer (time: TimeSpan) (player: MediaPlayer) (media: Media) =
    let offSet = settings.repeat.offset
    let repeatTime = settings.repeat.time

    let startTime =
        int64 time.TotalMilliseconds - offSet
        |> max 0L
        |> toSecf

    let endTime =
        int64 time.TotalMilliseconds + offSet
        |> min media.Duration
        |> toSecf

    media.AddOption $":repeat"
    media.AddOption ":no-play-and-pause"
    media.AddOption $":start-time={startTime}"
    media.AddOption $":stop-time={endTime}"
    media.AddOption $":input-repeat={repeatTime}"

    playAsync player () media

let randomize (player: MediaPlayer) (subPlayer: MediaPlayer) (playListUri: Uri) =
    taskResult {
        for p in [ player; subPlayer ] do
            if p.IsPlaying then do! stopAsync p ()

        let random = Random()

        let! playList = loadPlayList playListUri

        let media =
            playList.SubItems
            |> Seq.item (random.Next playList.SubItems.Count)

        let! _ = media.Parse MediaParseOptions.ParseNetwork
        let media' = media.Duplicate()


        let rTime =
            random.Next(settings.randomize.min, int media.Duration - settings.randomize.maxOffset)
            |> int64

        media.AddOption ":play-and-pause"
        media.AddOption $":start-time={rTime - settings.repeat.offset |> toSecf}"
        media.AddOption $":stop-time={rTime |> toSecf}"

        do! playAsync player () media

        do!
            Async.Sleep settings.randomize.SleepTime.onPlay
            |> Async.Ignore

        do! subscribeThumbnailPlayer (TimeSpan.FromMilliseconds(float rTime)) subPlayer media'

        if player.State = VLCState.Buffering then
            do!
                player.Media.StateChanged
                |> Async.AwaitEvent
                |> Async.Ignore

        do!
            Async.Sleep settings.randomize.SleepTime.onComplated
            |> Async.Ignore

        return RandomizeSuccess
    }
    |> TaskResult.foldResult id RandomizeFailed


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
