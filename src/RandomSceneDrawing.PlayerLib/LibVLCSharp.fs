namespace RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp
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
               "-vvv"
#else
               "-q"
#endif
               "--reset-plugins-cache"
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

    let initPlayer () =
        new MediaPlayer(libVLC)
        |> tap (fun p ->
            float32 config.SubPlayer.Rate
            |> p.SetRate
            |> ignore
        )

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


    module Media =
        let ofUri source = new Media(libVLC, uri = source)

        let inline parseAsync mediaParseOptions (media: Media) =
            taskResult {
                match! media.ParseAsync mediaParseOptions with
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
                    match media.TrackList TrackType.Video with
                    | t when t.Count = 0u -> Error "Target is Not Video."
                    | t ->
                        ((0u, 0u), t)
                        ||> (Seq.fold) (fun (w, h) t -> (max w t.Data.Video.Width, max h t.Data.Video.Height))
                        |> Ok
            }

        let inline addOptions (media: Media) options =
            for opt in options do
                media.AddOption(option = opt)

    let playAsync (player: MediaPlayer) media =
        taskResult {
            player.Media <- media

            do!
                player.PlayAsync()
                |> TaskResult.requireTrue "Play Failed."

            return! MediaInfo.ofMedia media

        }

    let pauseAsync (player: MediaPlayer) =
        task {
            do! player.PauseAsync()

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

    let stopAsync (player: MediaPlayer) =
        task {
            return!
                player.StopAsync()
                |> TaskResult.requireTrue "Play Failed."

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
