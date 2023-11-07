module RandomSceneDrawing.Platform

open System
open System.IO
open System.Collections.Generic

open type System.Environment

open Avalonia.Controls
open Avalonia.Dialogs
open Avalonia.Platform.Storage
open Avalonia.Controls.Notifications
open Avalonia.Threading

open Avalonia.FuncUI.DSL

open LibVLCSharp.Shared

open FSharpPlus
open FSharp.Control
open FsToolkit.ErrorHandling

open RandomSceneDrawing.Types



let list (fsCollection: 'T seq) = List<'T> fsCollection

let showInfomationAsync (window: MainWindow) msg =
    fun _ -> task {
        match msg with
        | InfoMsg info ->
            Notification("Info", info, NotificationType.Information)
            |> window.NotificationManager.Show
        | ErrorMsg err ->
            Notification("Error!!", err, NotificationType.Error)
            |> window.NotificationManager.Show
    }
    |> UIThread.invokeAsync DispatcherPriority.Send

open Player

let selectMediaAsync (window: Window) = task {
    let provier = window.StorageProvider
    let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Videos)

    let! result =
        provier.OpenFilePickerAsync(
            FilePickerOpenOptions(
                Title = "Open Video File",
                AllowMultiple = false,
                SuggestedStartLocation = location,
                FileTypeFilter = [
                    FilePickerFileType(
                        "Open Video File",
                        Patterns = [ "*mp4"; "*mkv" ],
                        AppleUniformTypeIdentifiers = [ "public.data" ]
                    )
                ]
            )
        )

    match List.ofSeq result with
    | [ picked ] -> return (PlayerLib.LibVLCSharp.Media.ofUri >> Ok) picked.Path
    | _ -> return Error "Conceled"
}


let playAsync window (player: MediaPlayer) = taskResult {
    let! media = selectMediaAsync window
    return! PlayerLib.LibVLCSharp.playAsync player media
}

let playerApi (window: MainWindow) = {
    playAsync = playAsync window
    pauseAsync = PlayerLib.LibVLCSharp.pauseAsync
    stopAsync = PlayerLib.LibVLCSharp.stopAsync
    showInfomation = showInfomationAsync window
}

open RandomSceneDrawing.Types.ErrorTypes

let pickPlayListAsync (window: Window) () = task {

    let! suggestedStartLocation =
        GetFolderPath(SpecialFolder.MyVideos)
        |> window.StorageProvider.TryGetFolderFromPathAsync

    let! result =
        FilePickerOpenOptions(
            Title = "Open Playlist",
            AllowMultiple = false,
            SuggestedStartLocation = suggestedStartLocation,
            FileTypeFilter = list [ FilePickerFileType("Playlist", Patterns = list [ "*.xspf" ]) ]
        )
        |> window.StorageProvider.OpenFilePickerAsync

    match Seq.tryHead result with
    | Some s -> return Ok(s.Path.LocalPath)
    | None -> return Error Canceled
}

let pickSnapshotFolderAsync (window: Window) () = task {
    let provier = window.StorageProvider
    let! location = provier.TryGetWellKnownFolderAsync(WellKnownFolder.Pictures)

    let! result =
        provier.OpenFolderPickerAsync(
            FolderPickerOpenOptions(
                Title = "Select SnapShot save root folder",
                SuggestedStartLocation = location,
                AllowMultiple = false
            )
        )

    match List.ofSeq result with
    | [ picked ] -> return Ok picked.Path.LocalPath
    | _ -> return Error Canceled
}

let settingsApi (window: MainWindow) : DrawingSettings.Api = {
    validateMediaInfo = PlayerLib.RandomizeInfoDto.validate
    parsePlayListFile = PlayerLib.RandomizeInfoDto.parsePlayListFile
    pickPlayList = pickPlayListAsync window
    pickSnapshotFolder = pickSnapshotFolderAsync window
    showInfomation = showInfomationAsync window
}

let stepAsync () = async { do! Async.Sleep 1000 }

let createCurrentSnapShotFolderAsync root =
    let unfolder state =
        match state with
        | -1 -> None
        | _ ->
            let path =
                [| root; DateTime.Now.ToString "yyyyMMdd"; $"%03i{state}" |] |> Path.Combine

            match Directory.Exists path with
            | true -> Some(path, state + 1)
            | false ->
                Directory.CreateDirectory path |> ignore
                Some(path, -1)

    taskResult { return Seq.unfold unfolder 0 |> Seq.last }

let copySubVideoAsync dest = taskResult { File.Copy(PlayerLib.Randomize.destination', dest) }


let randomizeAsync randomizeSource (player: MediaPlayer) (subPlayer: MediaPlayer) = taskResult {
    let! resultInfo = PlayerLib.Randomize.initSourceAsync randomizeSource player subPlayer

    do!
        fun () -> taskResult { do! PlayerLib.Randomize.startSublayerAsync subPlayer }
        |> UIThread.invokeAsync DispatcherPriority.Background

    do!
        fun () -> taskResult { do! PlayerLib.Randomize.startMainPlayerAsync player }
        |> UIThread.invokeAsync DispatcherPriority.Background

    do!
        fun () -> taskResult {
            do! PlayerLib.LibVLCSharp.seekAsync resultInfo.Position player
            player.NextFrame()
        }
        |> UIThread.invokeAsync DispatcherPriority.Background


    return resultInfo
}


let mainApi (window: MainWindow) : Main.Api<'player> = {
    step = stepAsync
    randomize = randomizeAsync
    createSnapShotFolder = createCurrentSnapShotFolderAsync
    takeSnapshot = PlayerLib.LibVLCSharp.takeSnapshot
    copySubVideo = copySubVideoAsync
    showInfomation = showInfomationAsync window
}
