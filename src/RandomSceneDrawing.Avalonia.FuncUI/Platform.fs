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
        match! PlayerLib.playAsync player PlaySuccess media with
        | Ok msg ->
            return
                msg
                    { Title = media.Meta MetadataType.Title
                      Duration = float media.Duration |> TimeSpan.FromMilliseconds }
        | Error e -> return PlayFailed e
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

        | _ -> return PlayCandeled
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

let toCmd (window: MainWindow) cmdMsg =

    match cmdMsg with
    | Play player -> Cmd.OfTask.either (selectMediaAndPlayAsync window) player id PlayFailed
    | Pause player ->
        Cmd.OfAsyncImmediate.either (PlayerLib.togglePauseAsync player) (Playing, Paused) PauseSuccess PauseFailed
    | Stop player -> Cmd.OfAsyncImmediate.either (PlayerLib.stopAsync player) StopSuccess id StopFailed
    | Randomize (player, subPlayer, pl) ->
        let randomize pl =
            PlayerLib.randomize player subPlayer pl
            |> Async.AwaitTask

        Cmd.OfAsyncImmediate.either randomize (Uri pl) id RandomizeFailed
    | SelectPlayListFilePath ->
        Cmd.OfAsyncImmediate.either selectPlayListFileAsync window id SelectPlayListFilePathFailed
    | SelectSnapShotFolderPath ->
        Cmd.OfAsyncImmediate.either selectSnapShotFolderAsync window id SelectSnapShotFolderPathFailed
    | CreateCurrentSnapShotFolder root -> createCurrentSnapShotFolder root |> Cmd.ofMsg
    | TakeSnapshot (player, path) ->
        async {
            do!
                Text.RegularExpressions.Regex.Replace(path, "png", "mp4")
                |> PlayerLib.copySubVideo
                |> Async.AwaitTask

            match PlayerLib.takeSnapshot (PlayerLib.getSize player) 0u path with
            | Some path -> return TakeSnapshotSuccess
            | None -> return TakeSnapshotFailed(SnapShotFailedException "Snapshotに失敗しました。")
        }
        |> Cmd.OfAsyncImmediate.result
    | StartDrawing ->
        Notification("Start", "Start Drawing.", NotificationType.Information)
        |> window.NotificationManager.Show

        startTimer StartDrawingSuccess
    | StopDrawing -> stopTimer StopDrawingSuccess |> Cmd.OfFunc.result
    | ShowErrorInfomation message ->
        showErrorNotification window.NotificationManager message ShowErrorInfomationSuccess
        |> Cmd.OfAsyncImmediate.result

let api (window: MainWindow) : Api =

    { playAsync = selectMediaAndPlayAsync window
      pauseAsync =
        fun player ->
            PlayerLib.togglePauseAsync player (Playing, Paused)
            |> Async.map PauseSuccess
            |> Async.StartAsTask
      stopAsync =
        fun player ->
            PlayerLib.stopAsync player StopSuccess
            |> Async.StartAsTask
      randomizeAsync = fun mp sp urlStr -> PlayerLib.randomize mp sp (Uri urlStr)
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
