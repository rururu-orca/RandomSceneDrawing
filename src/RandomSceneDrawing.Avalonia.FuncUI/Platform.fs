module RandomSceneDrawing.Platform

open System
open System.IO
open System.Collections.Generic

open type System.Environment

open Avalonia.Controls
open Avalonia.Controls.Notifications
open Avalonia.Threading

open Elmish
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Hosts

open LibVLCSharp

open FSharpPlus
open FSharp.Control
open FSharp.Control.Reactive
open FsToolkit.ErrorHandling

open RandomSceneDrawing.Types


let private drawingDisporsables = Disposable.Composite

let setupTimer dispatch =
    DispatcherTimer.Run(
        (fun () ->
            dispatch Tick
            true),
        TimeSpan.FromSeconds 1.0
    )
    |> Disposable.disposeWith drawingDisporsables

let startTimer onSuccess =
    Cmd.batch [
        Cmd.ofSub setupTimer
        Cmd.ofMsg onSuccess
    ]

let stopTimer onSuccess =
    drawingDisporsables.Clear()
    onSuccess

let list (fsCollection: 'T seq) = List<'T> fsCollection

let selectPlayListFileAsync window =
    async {
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

        match! dialog.ShowAsync window |> Async.AwaitTask with
        | [| path |] -> return SelectPlayListFilePathSuccess path

        | _ -> return SelectPlayListFilePathCanceled
    }

let selectSnapShotFolderAsync window =
    async {

        let dialog =
            OpenFolderDialog(
                Title = "Select SnapShot save root folder",
                Directory = GetFolderPath(SpecialFolder.MyPictures)
            )

        match! dialog.ShowAsync window |> Async.AwaitTask with
        | notSelect when String.IsNullOrEmpty notSelect -> return SelectSnapShotFolderPathCandeled
        | folderPath -> return SelectSnapShotFolderPathSuccess folderPath

    }

let playAsync player media =
    task {
        match! PlayerLib.playAsync player () media with
        | Ok _ ->
            return
                Ok
                    { Title = media.Meta MetadataType.Title
                      Duration = float media.Duration |> TimeSpan.FromMilliseconds }
        | Error ex -> return Error ex.Message
    }
    |> Task.map Finished

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
        | [| path |] -> return (Uri >> Ok) path
        | _ -> return Error "Conceled"
    }

let getMediaAndPlay (player: MediaPlayer) uri =
    task {
        let media = PlayerLib.getMediaFromUri uri
        player.Media <- media

        match! player.PlayAsync() with
        | true ->
            return
                Ok
                    { Title = media.Meta MetadataType.Title
                      Duration = float media.Duration |> TimeSpan.FromMilliseconds }
        | false -> return Error "play failed."
    }


let selectMediaAndPlayAsync window player =
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

        match! dialog.ShowAsync window |> Async.AwaitTask with
        | [| path |] ->
            return!
                Uri path
                |> PlayerLib.getMediaFromUri
                |> playAsync player

        | _ -> return Finished(Error "Canceled")
    }

let createCurrentSnapShotFolder root =
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

    Seq.unfold unfolder 0
    |> Seq.last
    |> CreateCurrentSnapShotFolderSuccess

let showErrorNotification (notificationManager: IManagedNotificationManager) info msg =
    async {
        Notification("Error!!", info, NotificationType.Error)
        |> notificationManager.Show

        return msg
    }

let api (window: MainWindow) : Api =

    { playAsync = (selectMediaAndPlayAsync window) >> Task.map Play
      pauseAsync =
        fun player ->
            PlayerLib.togglePauseAsync player (Playing, Paused)
            |> Async.map PauseSuccess
            |> Async.StartAsTask
      stopAsync =
        fun player ->
            PlayerLib.stopAsync player StopSuccess
            |> Async.StartAsTask
      randomizeAsync =
        fun mp sp urlStr ->
            task {
                let! result = PlayerLib.randomize mp sp (Uri urlStr)
                return result
            }
      createCurrentSnapShotFolderAsync = fun root -> task { return createCurrentSnapShotFolder root }
      takeSnapshotAsync =
        fun (player, path) ->
            task {
                do!
                    Text.RegularExpressions.Regex.Replace(path, "png", "mp4")
                    |> PlayerLib.copySubVideo
                    |> Async.AwaitTask

                match PlayerLib.takeSnapshot (PlayerLib.getSize player) 0u path with
                | Some path -> return TakeSnapshotSuccess
                | None -> return TakeSnapshotFailed(SnapShotFailedException "Snapshotに失敗しました。")
            }
      startDrawing =
        fun _ ->
            Notification("Start", "Start Drawing.", NotificationType.Information)
            |> window.NotificationManager.Show

            startTimer StartDrawingSuccess
      stopDrawingAsync = fun _ -> task { return stopTimer StopDrawingSuccess }
      selectPlayListFilePathAsync =
        fun _ ->
            selectPlayListFileAsync window
            |> Async.StartAsTask
      selectSnapShotFolderPathAsync =
        fun _ ->
            selectSnapShotFolderAsync window
            |> Async.StartAsTask
      showErrorAsync =
        fun message ->
            showErrorNotification window.NotificationManager message ShowErrorInfomationSuccess
            |> Async.StartAsTask }

let onClosed (window: HostWindow) dispatch =
    window.Closed
    |> Observable.add (fun e -> dispatch WindowClosed)

let subs (window: HostWindow) model =
    Cmd.batch [
        Cmd.ofSub (onClosed window)
        Cmd.ofSub (PlayerLib.timeChanged model.Player)
        Cmd.ofSub (PlayerLib.playerBuffering model.Player)
    ]

open Main

let showInfomation (window: MainWindow) msg =
    task {
        match msg with
        | InfoMsg info ->
            Notification("Info", info, NotificationType.Information)
            |> window.NotificationManager.Show
        | ErrorMsg err ->
            Notification("Error!!", err, NotificationType.Error)
            |> window.NotificationManager.Show
    }

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
      showInfomation = showInfomation window }

open Player

let playAsync' window (player: MediaPlayer) =
    taskResult {
        let! uri = selectMediaAsync window
        return! getMediaAndPlay player uri
    }

let pauseAsync' (player: MediaPlayer) =
    task {
        do! player.PauseAsync()

        return
            Ok
                { Title = player.Media.Meta MetadataType.Title
                  Duration =
                    float player.Media.Duration
                    |> TimeSpan.FromMilliseconds }
    }

let stopAsync' (player: MediaPlayer) =
    task {
        match! player.StopAsync() with
        | true -> return Ok()
        | false -> return Error "stop failed."
    }


let playerApi (window: MainWindow) =
    { playAsync = playAsync' window
      pauseAsync = pauseAsync'
      stopAsync = stopAsync'
      showInfomation = showInfomation window }


let mainApi (window: MainWindow) : Main.Api<'player> =
    { step = fun _ -> async { do! Async.Sleep 1000 }
      randomize = fun _ _ -> task { return Ok() }
      createSnapShotFolder = fun _ -> task { return Ok() }
      takeSnapshot = fun _ _ -> task { return Ok() }
      showInfomation = showInfomation window }
