module RandomSceneDrawing.Platform

open System
open System.IO
open System.Collections.Generic

open type System.Environment

open Avalonia.Controls
open Avalonia.Controls.Notifications

open Avalonia.FuncUI.DSL

open LibVLCSharp

open FSharpPlus
open FSharp.Control
open FsToolkit.ErrorHandling

open RandomSceneDrawing.Types
open Main

let list (fsCollection: 'T seq) = List<'T> fsCollection


let showInfomationAsync (window: MainWindow) msg =
    task {
        match msg with
        | InfoMsg info ->
            Notification("Info", info, NotificationType.Information)
            |> window.NotificationManager.Show
        | ErrorMsg err ->
            Notification("Error!!", err, NotificationType.Error)
            |> window.NotificationManager.Show
    }

open Player

let selectMediaAsync window =
    task {
        let dialog =
            OpenFileDialog(
                Title = "Open Video File",
                AllowMultiple = false,
                Directory = GetFolderPath(SpecialFolder.MyVideos),
                Filters =
                    list [
                        FileDialogFilter(Name = "Video", Extensions = list [ "mp4"; "mkv" ])
                    ]
            )

        match! dialog.ShowAsync window with
        | [| path |] -> return (Uri >> PlayerLib.getMediaFromUri >> Ok) path
        | _ -> return Error "Conceled"
    }


let playAsync window (player: MediaPlayer) =
    taskResult {
        let! media = selectMediaAsync window
        player.Media <- media

        do!
            player.PlayAsync()
            |> TaskResult.requireTrue "Play Failed."

        return! PlayerLib.MediaInfo.ofMedia media

    }

let pauseAsync (player: MediaPlayer) =
    task {
        do! player.PauseAsync()

        return! PlayerLib.MediaInfo.ofPlayer player
    }

let stopAsync (player: MediaPlayer) =
    task {
        match! player.StopAsync() with
        | true -> return Ok()
        | false -> return Error "stop failed."
    }

let playerApi (window: MainWindow) =
    { playAsync = playAsync window
      pauseAsync = pauseAsync
      stopAsync = stopAsync
      showInfomation = showInfomationAsync window }

let createCurrentSnapShotFolderAsync root =
    let unfolder state =
        match state with
        | -1 -> None
        | _ ->
            let path =
                [| root
                   DateTime.Now.ToString "yyyyMMdd"
                   $"%03i{state}" |]
                |> Path.Combine

            match Directory.Exists path with
            | true -> Some(path, state + 1)
            | false ->
                Directory.CreateDirectory path |> ignore
                Some(path, -1)

    taskResult { return Seq.unfold unfolder 0 |> Seq.last }

open RandomSceneDrawing.Types.ErrorTypes

let pickPlayListAsync window () =
    task {
        let dialog =
            OpenFileDialog(
                Title = "Open Playlist",
                AllowMultiple = false,
                Directory = GetFolderPath(SpecialFolder.MyVideos),
                Filters =
                    list [
                        FileDialogFilter(Name = "Playlist", Extensions = list [ "xspf" ])
                    ]
            )

        match! dialog.ShowAsync window with
        | [| path |] -> return Ok path

        | _ -> return Error Canceled
    }

let pickSnapshotFolderAsync window () =
    task {

        let dialog =
            OpenFolderDialog(
                Title = "Select SnapShot save root folder",
                Directory = GetFolderPath(SpecialFolder.MyPictures)
            )

        match! dialog.ShowAsync window with
        | notSelect when String.IsNullOrEmpty notSelect -> return Error Canceled
        | folderPath -> return Ok folderPath

    }

let settingsApi (window: MainWindow) : DrawingSettings.Api =
    { pickPlayList = pickPlayListAsync window
      pickSnapshotFolder = pickSnapshotFolderAsync window
      showInfomation = showInfomationAsync window }

let stepAsync () = async { do! Async.Sleep 1000 }

let takeSnapShotAsync player path =
    taskResult {
        do!
            PlayerLib.takeSnapshot player 0u path
            |> TaskResult.ignore
    }

let copySubVideoAsync dest =
    taskResult { File.Copy(PlayerLib.destination', dest) }

let mainApi (window: MainWindow) : Main.Api<'player> =
    { step = stepAsync
      randomize = PlayerLib.Randomize.run
      createSnapShotFolder = createCurrentSnapShotFolderAsync
      takeSnapshot = takeSnapShotAsync
      copySubVideo = copySubVideoAsync
      showInfomation = showInfomationAsync window }
