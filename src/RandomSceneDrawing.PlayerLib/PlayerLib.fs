namespace RandomSceneDrawing.PlayerLib

open System
open LibVLCSharp
open LibVLCSharp.Shared
open System.IO
open FsToolkit.ErrorHandling
open FsToolkit.ErrorHandling.Operator.TaskResult
open RandomSceneDrawing.Main.ValueTypes
open RandomSceneDrawing.Util
open FSharp.Control
open Utils

module Randomize =
    let tempSavePath = $"{Path.GetTempPath()}/RandomSceneDrawing"

    if not (Directory.Exists tempSavePath) then
        Directory.CreateDirectory tempSavePath |> ignore

    let tempVideo = "trimed.mp4"
    let tempVideo' = "trimed_sub.mp4"

    /// 一時ファイル名
    let destination = $"{tempSavePath}/{tempVideo}"

    let destination' = $"{tempSavePath}/{tempVideo'}"

    let parseRandomizeSource (random: Random) rs = taskResult {
        match rs with
        | PlayList path ->
            let! playList =
                playListFilePath.ToDto path
                |> (Uri >> Media.ofUri)
                |> Media.parseAsync MediaParseOptions.ParseNetwork

            let media = playList.SubItems |> Seq.item (random.Next playList.SubItems.Count)

            return! Ok media
        | RandomizeInfos infos ->
            let idx = random.Next infos.Length
            return! (Uri >> Media.ofUri >> Ok) infos[idx].Path
    }


    let inline getRescaleParam (media: Media) = taskResult {
        let! originWidth, originHeight = Media.videoSize media

        if
            float originWidth / float originHeight
            <= float config.SubPlayer.Width / float config.SubPlayer.Height
        then
            return $"-1:{config.SubPlayer.Height}"
        else
            return $"{config.SubPlayer.Width}:-1"
    }

    let inline replace pattern replacement input =
        Text.RegularExpressions.Regex.Replace(input, pattern, replacement = replacement)

    let inline getRandomizeResult (mainMedia: Media) mainPath (subMedia: Media) subPath startTime endTime position = taskResult {
        let getInfo media = taskResult {
            let! m = Media.parseAsync MediaParseOptions.ParseLocal media
            return! MediaInfo.ofMedia m
        }

        let! mainInfo = getInfo mainMedia
        and! subInfo = getInfo subMedia

        return {
            MainInfo = mainInfo
            MainPath = mainPath
            SubInfo = subInfo
            SubPath = subPath
            StartTime = TimeSpan.FromSeconds startTime
            EndTime = TimeSpan.FromSeconds endTime
            Position = TimeSpan.FromSeconds position
        }
    }

    let inline initSourceAsync randomizeSource (player: MediaPlayer) (subPlayer: MediaPlayer) = taskResult {
        // すでに再生済みなら停止
        for p in [ player; subPlayer ] do
            if p.IsPlaying then
                do! stopAsync p

        // 再生動画、時間を設定
        let random = Random()


        let! media =
            parseRandomizeSource random randomizeSource
            >>= Media.parseAsync MediaParseOptions.ParseNetwork

        let! rescaleParam = getRescaleParam media

        let randomizeTime =
            random.Next(config.Randomize.MinStartMsec, int media.Duration - config.Randomize.MaxEndMsec)
            |> int64

        let repeatOffset = int64 config.SubPlayer.RepeatDuration / 2L

        let startTime = randomizeTime - repeatOffset |> max 0L |> toSecf

        let endTime = randomizeTime + repeatOffset |> min media.Duration |> toSecf

        let! path = Media.pathOrUri media |> TaskResult.map (replace @"^/" "")

        // メディア生成
        do! FFmpeg.encodeAsync startTime endTime rescaleParam path destination destination'

        // 生成した一時ファイルのMedia
        let media = new Media(libVLC, destination, FromType.FromPath)
        /// サブプレイヤー用一時ファイル
        let media' = new Media(libVLC,destination', FromType.FromPath)

        // メインプレイヤーの設定
        let positionTime = Math.Round((endTime - startTime) / 2.0, 2)

        player.Media <- media


        subPlayer.Media <- media'

        return! getRandomizeResult media destination media' destination' startTime endTime positionTime
    }

    let inline startMainPlayerAsync (player: MediaPlayer) = taskResult {
        [ 
          ":start-paused"
          ":clock-jitter=0"
          ":clock-synchro=0"
          ":input-repeat=65535"
        ]
        |> List.iter player.Media.AddOption
        do! player.Play() |> Result.requireTrue "Main Player: Play failed."
    }

    let inline startSublayerAsync (subPlayer: MediaPlayer) = taskResult {
        [ 
         ":no-start-paused"
         ":clock-jitter=0"
         ":clock-synchro=0"
         ":input-repeat=65535"
        ]
        |> List.iter subPlayer.Media.AddOption
        do! subPlayer.Play() |> Result.requireTrue "Sub Player: Play failed."
    }