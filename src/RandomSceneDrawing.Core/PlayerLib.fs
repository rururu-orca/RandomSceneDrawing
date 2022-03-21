module RandomSceneDrawing.PlayerLib

open System
open System.IO
open System.Diagnostics
open Cysharp.Diagnostics
open LibVLCSharp
open FsToolkit.ErrorHandling
open Types
open Util
open FSharp.Control
open FSharpPlus
open System.Threading
open System.Threading.Tasks

[<AutoOpen>]
module Helper =
    let toSecf mSec = float mSec / 1000.0

    let internalAsync sub unsub action (tcs: TaskCompletionSource) (ctr: CancellationTokenRegistration) =
        task {
            try
                sub ()
                action ()
                return! tcs.Task.ConfigureAwait false
            finally
                unsub ()
                ctr.Dispose()
        }


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
#else
           "-q"
#endif
           "--hw-dec"
           "--codec=nvdec,any"
           "--dec-dev=nvdec"
           "--packetizer=hevc,any"
           "--no-snapshot-preview"
           "--no-audio"
           "--aout=none"
           "--no-drop-late-frames"
           "--no-skip-frames" |]

    new LibVLC(options)

let initPlayer () = new MediaPlayer(libVLC)

let initSubPlayer () =
    let caching = uint config.SubPlayer.RepeatDuration

    new MediaPlayer(libVLC, FileCaching = caching, NetworkCaching = caching)
    |> tap (fun p ->
        float32 config.SubPlayer.Rate
        |> p.SetRate
        |> ignore)

let isPlayingOrPaused (player: MediaPlayer) =
    match enum<VLCState> (int player.Scale) with
    | VLCState.Playing
    | VLCState.Paused -> true
    | _ -> false


let getMediaFromUri source = new Media(libVLC, uri = source)

module MediaInfo =
    let inline ofMedia (media: Media) =
        taskResult {
            return
                { Title = media.Meta MetadataType.Title
                  Duration = float media.Duration |> TimeSpan.FromMilliseconds }
        }

    let inline ofPlayer (player: MediaPlayer) =
        taskResult {
            let! media =
                match player.Media with
                | null -> Error "Player is not set Media."
                | md -> Ok md

            return! ofMedia media
        }

let loadPlayList source =
    task {
        let playList = new Media(libVLC, uri = source)

        let! _ = playList.ParseAsync MediaParseOptions.ParseNetwork

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
    task {

        let msg = String.Format(config.PlayerLib.PlayFailedMsg, media.Mrl)

        let sub = player.Playing |> Async.AwaitEvent |> Async.Ignore

        if player.Play media then
            do! sub
            return Ok onSuccess
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
        do!
            player.StopAsync()
            |> Async.AwaitTask
            |> Async.Ignore

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

        match! media.ParseAsync MediaParseOptions.ParseNetwork with
        | MediaParsedStatus.Done -> do! Ok()
        | other -> do! Error(exn $"Media Parse %A{other}")

        let! originWidth, originHeight =
            match media.TrackList TrackType.Video with
            | t when t.Count = 1u ->
                let t = t[0u]
                Ok(t.Data.Video.Width, t.Data.Video.Height)
            | _ -> Error(exn "Target is Not Video.")

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
                    return (exn (stderr.ToString()) |> Error)
            }

        // キャプチャ設定
        let mrl = Uri(media.Mrl)

        let path =
            if mrl.IsFile then
                mrl.LocalPath
            else
                mrl.AbsoluteUri

        let path = Text.RegularExpressions.Regex.Replace(path, @"^/", "")

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

open Main.ValueTypes

module Randomize =

    let inline runFFmpeg args =
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
                let msg = $"%A{ex.ErrorOutput}\n\n{stderr}\n"
                return Error msg
        }

    let inline parseAsync (media: Media) =
        taskResult {
            match! media.ParseAsync MediaParseOptions.ParseNetwork with
            | MediaParsedStatus.Done -> return! Ok()
            | other -> return! Error $"Media Parse %A{other}"
        }

    let inline getMediaPath (media: Media) =
        taskResult {
            match Uri(media.Mrl) with
            | file when file.IsFile -> return file.LocalPath
            | other -> return other.AbsoluteUri
        }

    let inline getRescaleParam (media: Media) =
        taskResult {
            let! originWidth, originHeight =
                match media.TrackList TrackType.Video with
                | t when t.Count = 1u ->
                    let t = t[0u]
                    Ok(t.Data.Video.Width, t.Data.Video.Height)
                | _ -> Error "Target is Not Video."

            if float originWidth / float originHeight
               <= float config.SubPlayer.Width
                  / float config.SubPlayer.Height then
                return $"-1:{config.SubPlayer.Height}"
            else
                return $"{config.SubPlayer.Width}:-1"
        }

    let inline replace pattern replacement input =
        Text.RegularExpressions.Regex.Replace(input, pattern, replacement = replacement)

    let inline trimMediaAsync startTime endTime inputPath destinationPath =
        [ $"-loglevel warning -y -ss %.3f{startTime} -to %.3f{endTime} -copyts -i \"{inputPath}\" -c:v copy -an \"{destinationPath}\"" ]
        |> runFFmpeg

    let inline resizeMediaAsync inputPath rescaleParam destinationPath =
        [ "-loglevel warning -y -hwaccel cuda -hwaccel_output_format cuda -init_hw_device vulkan=vk:0 -filter_hw_device vk"
          $"-copyts -i \"{inputPath}\""
          $"-vf \"hwupload,libplacebo={rescaleParam}:p010le,hwupload_cuda\""
          $"-c:v hevc_nvenc -an \"{destinationPath}\"" ]
        |> runFFmpeg

    let inline getRandomizeResult (mainMedia: Media) mainPath (subMedia: Media) subPath startTime endTime position =
        taskResult {
            let! mainInfo = MediaInfo.ofMedia mainMedia
            and! subInfo = MediaInfo.ofMedia subMedia

            return
                { Main =
                    { MediaInfo = mainInfo
                      Path = mainPath }
                  Sub = { MediaInfo = subInfo; Path = subPath }
                  StartTime = TimeSpan.FromSeconds startTime
                  EndTime = TimeSpan.FromSeconds endTime
                  Position = TimeSpan.FromSeconds position }
        }


    let run (Main.PlayListFilePath playListPath) (player: MediaPlayer) (subPlayer: MediaPlayer) =
        taskResult {
            // すでに再生済みなら停止
            for p in [ player; subPlayer ] do
                if p.IsPlaying then do! stopAsync p ()

            // 再生動画、時間を設定
            let random = Random()

            let! playList = (Uri >> loadPlayList) playListPath

            let media =
                playList.SubItems
                |> Seq.item (random.Next playList.SubItems.Count)

            do! parseAsync media

            let! rescaleParam = getRescaleParam media

            let randomizeTime =
                random.Next(config.Randomize.MinStartMsec, int media.Duration - config.Randomize.MaxEndMsec)
                |> int64

            let repeatOffset = int64 config.SubPlayer.RepeatDuration / 2L

            let startTime = randomizeTime - repeatOffset |> max 0L |> toSecf

            let endTime =
                randomizeTime + repeatOffset
                |> min media.Duration
                |> toSecf

            let! path =
                getMediaPath media
                |> TaskResult.map (replace @"^/" "")

            // メディア生成
            do! trimMediaAsync startTime endTime path destination

            do! resizeMediaAsync destination rescaleParam destination'

            /// 生成した一時ファイルのMedia
            let media = new Media(libVLC, destination, FromType.FromPath)
            /// サブプレイヤー用一時ファイル
            let media' = new Media(libVLC, destination', FromType.FromPath)


            // メインプレイヤーの設定、再生
            media.AddOption ":no-audio"
            media.AddOption ":start-paused"
            media.AddOption ":clock-jitter=0"
            media.AddOption ":clock-synchro=0"
            let positionTime = Math.Round(startTime + ((endTime - startTime) / 2.0), 2)
            media.AddOption $":start-time=%.2f{positionTime}"

            player.Media <- media

            do!
                player.PlayAsync()
                |> TaskResult.requireTrue "Main Player: Play failed."

            // サブプレイヤーの設定、再生
            media'.AddOption ":no-start-paused"
            media'.AddOption ":clock-jitter=0"
            media'.AddOption ":clock-synchro=0"
            media'.AddOption $":input-repeat=65535"
            subPlayer.Media <- media'

            do!
                subPlayer.PlayAsync()
                |> TaskResult.requireTrue "Sub Player: Play failed."

            do! Async.Sleep 50 |> Async.Ignore

            if player.State = VLCState.Buffering then
                do!
                    player.Media.StateChanged
                    |> Async.AwaitEvent
                    |> Async.Ignore

            do! Async.Sleep 50 |> Async.Ignore

            return! getRandomizeResult media destination media' destination' startTime endTime positionTime
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

let copySubVideo dest = task { File.Copy(destination', dest) }
