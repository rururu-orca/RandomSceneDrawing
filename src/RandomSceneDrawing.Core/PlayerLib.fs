module RandomSceneDrawing.PlayerLib

open System
open System.IO
open System.Diagnostics
open Cysharp.Diagnostics
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open Types
open Util
open FSharp.Control
open FSharpPlus
open System.Threading.Tasks

[<AutoOpen>]
module Helper =
    let toSecf mSec = float mSec / 1000.0


let tempSavePath = $"{Path.GetTempPath()}/RandomSceneDrawing"

if not (Directory.Exists tempSavePath) then
    Directory.CreateDirectory tempSavePath |> ignore

let tempVideo = "trimed.mp4"
let tempVideo' = "trimed_sub.mp4"

/// 一時ファイル名
let destination = $"{tempSavePath}/{tempVideo}"

let destination' = $"{tempSavePath}/{tempVideo'}"

let initialize () = Core.Initialize()

let libVLC =
    let options =
        [|
#if DEBUG
           "-vvv"
#endif
           "--no-snapshot-preview"
           "--audio-time-stretch"
           "--no-audio"
           "--aout=none"
           "--no-drop-late-frames"
           "--no-skip-frames"
           "--avcodec-skip-frame"
           "--avcodec-hw=any" |]

    new LibVLC(options)

let initPlayer () =
    new MediaPlayer(libVLC, EnableHardwareDecoding = true)

let initSubPlayer () =
    let caching = uint config.SubPlayer.RepeatDuration

    new MediaPlayer(libVLC, FileCaching = caching, NetworkCaching = caching, EnableHardwareDecoding = true)
    |> tap (fun p ->
        float32 config.SubPlayer.Rate
        |> p.SetRate
        |> ignore)


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

        let msg = String.Format(config.PlayerLib.PlayFailedMsg, media.Mrl)

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


let randomize (player: MediaPlayer) (subPlayer: MediaPlayer) (playListUri: Uri) =
    taskResult {
        // すでに再生済みなら停止
        for p in [ player; subPlayer ] do
            if p.IsPlaying then do! stopAsync p ()

        // 再生動画、時間を設定
        let random = Random()

        let! playList = loadPlayList playListUri

        let media =
            playList.SubItems
            |> Seq.item (random.Next playList.SubItems.Count)

        match! media.Parse MediaParseOptions.ParseNetwork with
        | MediaParsedStatus.Done -> do! Ok()
        | other -> do! Error(exn "Media Parse $%A{other}")

        let! originWidth, originHeight =
            media.Tracks
            |> Array.tryFind (fun t -> t.TrackType = TrackType.Video)
            |> Option.map (fun t -> Ok(t.Data.Video.Width, t.Data.Video.Height))
            |> Option.defaultValue (Error(exn "Target is Not Video."))

        let rescaleParam =
            if float originWidth / float originHeight
               <= float config.SubPlayer.Width
                  / float config.SubPlayer.Height then
                $"-1:{config.SubPlayer.Height}"
            else
                $"{config.SubPlayer.Width}:-1"

        let randomizeTime =
            random.Next(config.Randomize.MinStartMsec, int media.Duration - config.Randomize.MaxEndMsec)
            |> int64

        let repeatOffset = int64 config.SubPlayer.RepeatDuration / 2L

        let startTime = randomizeTime - repeatOffset |> max 0L |> toSecf

        let endTime =
            randomizeTime + repeatOffset
            |> min media.Duration
            |> toSecf

        let runFFmpeg args =
            task {
                let args' = String.concat " " args
                let startInfo = ProcessStartInfo(config.PlayerLib.FFmpegPath, Arguments = args')

                try
                    let struct (_, stdout, stderr) = ProcessX.GetDualAsyncEnumerable(startInfo)

                    do!
                        [| stdout.WriteLineAllAsync()
                           stderr.WriteLineAllAsync() |]
                        |> Task.WhenAll

                    return Ok()
                with
                | :? ProcessErrorException as ex ->
                    let msg = $"%A{ex.ErrorOutput}"
                    return (exn (msg, ex) |> Error)
            }

        // キャプチャ設定
        let mrl = Uri(media.Mrl)

        let path =
            if mrl.IsFile then
                mrl.LocalPath
            else
                mrl.AbsoluteUri

        // 録画実行
        do!
            [ $"-loglevel warning -y -ss %.3f{startTime} -to %.3f{endTime} -i \"{path}\" -c:v copy -an \"{destination}\"" ]
            |> runFFmpeg

        do!
            [ "-loglevel warning -y -hwaccel cuda -hwaccel_output_format cuda -init_hw_device vulkan=vk:0 -filter_hw_device vk"
              $"-i \"{destination}\""
              $"-vf \"hwupload,libplacebo={rescaleParam}:p010le,hwupload_cuda\""
              $"-c:v hevc_nvenc -an \"{destination'}\"" ]
            |> runFFmpeg

        /// 生成した一時ファイルのMedia
        let media = new Media(libVLC, destination, FromType.FromPath)
        /// サブプレイヤー用一時ファイル
        let media' = new Media(libVLC, destination', FromType.FromPath)

        // メインプレイヤーの設定、再生
        media.AddOption ":no-audio"
        media.AddOption ":start-paused"
        media.AddOption ":clock-jitter=0"
        media.AddOption ":clock-synchro=0"
        let time = Math.Round((endTime - startTime) / 2.0, 2)
        media.AddOption $":start-time=%.2f{time}"
        do! playAsync player () media

        // サブプレイヤーの設定、再生
        media'.AddOption ":no-start-paused"
        media'.AddOption ":clock-jitter=0"
        media'.AddOption ":clock-synchro=0"
        media'.AddOption $":input-repeat=65535"
        do! playAsync subPlayer () media'
        do! Async.Sleep 50 |> Async.Ignore

        if player.State = VLCState.Buffering then
            do!
                player.Media.StateChanged
                |> Async.AwaitEvent
                |> Async.Ignore

        do! Async.Sleep 50 |> Async.Ignore

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

let copySubVideo dest = task { File.Copy(destination', dest) }
