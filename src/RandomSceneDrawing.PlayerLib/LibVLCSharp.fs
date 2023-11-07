namespace RandomSceneDrawing.PlayerLib

open System
open System.Threading
open LibVLCSharp
open LibVLCSharp.Shared
open FsToolkit.ErrorHandling
open RandomSceneDrawing.Util
open FSharp.Control

module MediaInfo =
    open RandomSceneDrawing
    open DrawingSettings.ValueTypes

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

module LibVLCSharp =
    let libVLC =
        let options =
            [|
#if DEBUG
               "-vv"
#else
               "-q"
#endif
               "--sout-rtp-caching=1500"
               "--sout-udp-caching=1500"
               "--sout-rist-caching=1500"
               "--file-caching=1500"
               "--live-caching=1500"
               "--disc-caching=1500"
               "--network-caching=1500"
               "--sout-mux-caching=1500"
               "--reset-plugins-cache"
               "--no-snapshot-preview"
               "--no-audio"
               "--aout=none"
            //    "--no-drop-late-frames"
            //    "--no-skip-frames"
               " --prefetch-seek-threshold=65536"
               "--sout-avcodec-hurry-up"
               "--http-continuous"
               "--no-video-deco"
               |]

        new LibVLC(true,options)

    let initPlayer () =
        let caching = uint 600
        new MediaPlayer(libVLC,  EnableHardwareDecoding=true)
        |> tap (fun p ->
            (float32 config.SubPlayer.Rate) / 2.0f
            |> p.SetRate
            |> ignore
        )

    let initSubPlayer () =
        let caching = uint config.SubPlayer.RepeatDuration

        new MediaPlayer(libVLC ,EnableHardwareDecoding=true)
        |> tap (fun p ->
            float32 config.SubPlayer.Rate
            |> p.SetRate
            |> ignore)

    let isPlayingOrPaused (player: MediaPlayer) =
        match enum<VLCState> (int player.Scale) with
        | VLCState.Playing
        | VLCState.Paused -> true
        | _ -> false


    module Media =
        let ofUri source = new Media(libVLC,uri = source)

        let inline parseAsync mediaParseOptions (media: Media) =
            taskResult {
                match! media.Parse(mediaParseOptions) with
                | MediaParsedStatus.Done -> return! Ok media
                | other -> return! Error $"Media Parse %A{other}"
            }

        let inline pathOrUri (media: Media) =
            taskResult {
                match Uri(media.Mrl) with
                | file when file.IsFile -> return file.LocalPath
                | other -> return other.AbsoluteUri
            }

        let inline videoSize (media: Media) =
            taskResult {
                return!
                    (Error "Target is Not Video.", media.Tracks)
                    ||> Seq.fold (fun acc t ->
                        match acc,t.TrackType with
                        | Ok (w,h), TrackType.Video -> Ok ( max w t.Data.Video.Width, max h t.Data.Video.Height)
                        | Error _, TrackType.Video -> Ok ( t.Data.Video.Width, t.Data.Video.Height)
                        | _ ->  acc)
            }

        let inline addOptions (media: Media) options =
            for opt in options do
                media.AddOption(option = opt)

    let inline playAsync (player: MediaPlayer) media =
        taskResult {
            do!
                ThreadPool.QueueUserWorkItem(fun _->
                    player.Play media |> ignore
                )
                |> Result.requireTrue "Play Failed."

            return! MediaInfo.ofMedia media

        }
    let inline replayAsync (player: MediaPlayer) =
        taskResult {
            do!
                ThreadPool.QueueUserWorkItem(fun _->
                    player.Play() |> ignore
                )
                |> Result.requireTrue "Play Failed."
        }

    let inline pauseAsync (player: MediaPlayer) =
        taskResult {
            if not <| isNull player.Media then
                do!
                    ThreadPool.QueueUserWorkItem(fun _->
                        if player.State = VLCState.Ended then
                            player.Stop()
                        match player.State with
                        | VLCState.Paused
                        | VLCState.Stopped
                        | VLCState.Error ->
                            player.Play() |> ignore
                        | _ ->
                            player.Pause()
                    )
                    |> Result.requireTrue "Pause Failed."
            return! MediaInfo.ofPlayer player
        }

    let resumeAsync (player: MediaPlayer) onSuccess = async { return player.SetPause false }

    let togglePauseAsync (player: MediaPlayer) (onPlaying, onPaused) =
        async {

            player.Pause()

            if player.State = VLCState.Paused then
                return onPaused
            else
                return onPlaying
        }

    let inline stopAsync (player: MediaPlayer) =
        task {
            return
                ThreadPool.QueueUserWorkItem(fun _->
                    player.Stop()
                )
                |> Result.requireTrue "Stop Failed."
        }

    let inline seekAsync time (player: MediaPlayer) =
        taskResult {
            if player.IsSeekable then
                do! 
                    player.SeekableChanged
                    |> Event.filter ( fun e -> e.Seekable <> 0)
                    |> Async.AwaitEvent
                    |> Async.Ignore
            do!
                ThreadPool.QueueUserWorkItem(fun _->
                    player.SeekTo(time)
                )
                |> Result.requireTrue "Seek Failed."
        }

    let getSize (player: MediaPlayer) num =
        taskResult {
            let mutable px, py = 0u, 0u

            if player.Size(num, &px, &py) then
                return! Ok(px, py)
            else
                return! Error "Get player size failed."
        }

    let takeSnapshot (player: MediaPlayer) path =
        taskResult {
            let num = 0u
            let! (px, py) = getSize player num

            if player.TakeSnapshot(num, path, px, py) then
                return! Ok()
            else
                return! Error "Take snapshot failed."
        }

module RandomizeInfoDto =
    open LibVLCSharp
    open RandomSceneDrawing
    open DrawingSettings.ValueTypes

    let validate (dto: RandomizeInfoDto) =
        taskResult {
            let mediaUri = Uri dto.Path

            let opt =
                if mediaUri.IsFile then
                    MediaParseOptions.ParseLocal
                else
                    MediaParseOptions.ParseNetwork

            let! media =
                Media.ofUri mediaUri
                |> Media.parseAsync opt
                |> TaskResult.mapError (List.singleton)

            let dto' =
                { dto with
                    MediaInfo = { dto.MediaInfo with Duration = (float >> TimeSpan.FromMilliseconds) media.Duration } }

            let! _ = RandomizeInfoDto.validateTrimDurations dto'

            return dto'
        }
        |> Async.AwaitTask
        |> Async.RunSynchronously

    let parsePlayListFile path =
        taskResult {
            let mediaUri = (playListFilePath.ToDto >> Uri) path

            let opt =
                if mediaUri.IsFile then
                    MediaParseOptions.ParseLocal
                else
                    MediaParseOptions.ParseNetwork

            let! playList =
                Media.ofUri mediaUri
                |> Media.parseAsync opt
                |> TaskResult.mapError (List.singleton)

            return
                playList.SubItems
                |> Seq.map (fun m ->
                    { RandomizeInfoDto.Id = Guid.NewGuid()
                      TrimDurations =
                        [ { Start = TimeSpan.FromSeconds 3.0
                            End = TimeSpan.FromMilliseconds(float m.Duration - 3000.0) } ]
                      MediaInfo =
                        { Title = m.Meta MetadataType.Title
                          Duration = TimeSpan.FromMilliseconds(float m.Duration)}
                      Path = m.Mrl })
                |> Seq.toList
        }
