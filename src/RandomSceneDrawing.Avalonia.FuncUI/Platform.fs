module RandomSceneDrawing.Platform

open System
open System.IO
open System.Collections.Generic

open type System.Environment

open Avalonia.Controls
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
    fun _ ->
        task {
            match msg with
            | InfoMsg info ->
                Notification("Info", info, NotificationType.Information)
                |> window.NotificationManager.Show
            | ErrorMsg err ->
                Notification("Error!!", err, NotificationType.Error)
                |> window.NotificationManager.Show
        }
    |> UIThread.invokeAsync DispatcherPriority.Layout

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
        | [| path |] -> return (Uri >> PlayerLib.LibVLCSharp.Media.ofUri >> Ok) path
        | _ -> return Error "Conceled"
    }


let playAsync window (player: MediaPlayer) =
    taskResult {
        let! media = selectMediaAsync window
        return! PlayerLib.LibVLCSharp.playAsync player media
    }

let playerApi (window: MainWindow) =
    { playAsync = playAsync window
      pauseAsync = PlayerLib.LibVLCSharp.pauseAsync
      stopAsync = PlayerLib.LibVLCSharp.stopAsync
      showInfomation = showInfomationAsync window }

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
    { validateMediaInfo = PlayerLib.RandomizeInfoDto.validate
      parsePlayListFile = PlayerLib.RandomizeInfoDto.parsePlayListFile
      pickPlayList = pickPlayListAsync window
      pickSnapshotFolder = pickSnapshotFolderAsync window
      showInfomation = showInfomationAsync window }

let stepAsync () = async { do! Async.Sleep 1000 }

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

let copySubVideoAsync dest =
    taskResult { File.Copy(PlayerLib.Randomize.destination', dest) }


let randomizeAsync randomizeSource (player: MediaPlayer) (subPlayer: MediaPlayer) =
    taskResult {
        let! resultInfo = PlayerLib.Randomize.initSourceAsync randomizeSource player subPlayer

        do!        
            fun () ->
                taskResult {
                    do! PlayerLib.Randomize.startSublayerAsync subPlayer
                }                
            |> UIThread.invokeAsync DispatcherPriority.Background

        do!        
            fun () ->
                taskResult {
                    do! PlayerLib.Randomize.startMainPlayerAsync player
                }                
            |> UIThread.invokeAsync DispatcherPriority.Background

        do!        
            fun () ->
                taskResult {
                    do! PlayerLib.LibVLCSharp.seekAsync resultInfo.Position player
                    player.NextFrame()
                }                
            |> UIThread.invokeAsync DispatcherPriority.Background


        return resultInfo
    }


let mainApi (window: MainWindow) : Main.Api<'player> =
    { step = stepAsync
      randomize = randomizeAsync
      createSnapShotFolder = createCurrentSnapShotFolderAsync
      takeSnapshot = PlayerLib.LibVLCSharp.takeSnapshot
      copySubVideo = copySubVideoAsync
      showInfomation = showInfomationAsync window }
