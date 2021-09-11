module RandomSceneDrawing.Platform

open System
open System.Text
open System.Collections.Generic
open FSharpPlus
open LibVLCSharp.Shared
open LibVLCSharp.Avalonia.FuncUI

open Avalonia.FuncUI.DSL

open Elmish
open Avalonia
open Avalonia.Controls
open Avalonia.Dialogs
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Avalonia.Media
open Avalonia.Threading
open RandomSceneDrawing.Types

open type System.Environment


let private timer =
    DispatcherTimer DispatcherPriority.Render

let setupTimer dispatch =
    timer.Tick
    |> Observable.add (fun _ -> dispatch Tick)

let startTimer onSuccess =
    async {
        timer.Start()
        return onSuccess
    }

let stopTimer onSuccess =
    async {
        timer.Stop()
        return onSuccess
    }

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
        | [| path |] ->
            return SelectPlayListFilePathSuccess path

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
    async {
        match! PlayerLib.playAsync player PlaySuccess media with
        | Ok msg ->
            return
                msg
                    { Title = media.Meta MetadataType.Title
                      Duration = float media.Duration |> TimeSpan.FromMilliseconds }
        | Error e -> return PlayFailed e
    }

let selectMediaAndPlayAsync window player =
    async {
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

let toCmd window cmdMsg =
    match cmdMsg with
    | Play player -> Cmd.OfAsyncImmediate.either (selectMediaAndPlayAsync window) player id PlayFailed
    | Pause player ->
        Cmd.OfAsyncImmediate.either (PlayerLib.togglePauseAsync player) (Playing, Paused) PauseSuccess PauseFailed
    | Stop player -> Cmd.OfAsyncImmediate.either (PlayerLib.stopAsync player) StopSuccess id StopFailed
    | SelectPlayListFilePath -> Cmd.OfAsyncImmediate.either selectPlayListFileAsync window id SelectPlayListFilePathFailed
    | SelectSnapShotFolderPath -> Cmd.OfAsyncImmediate.either selectSnapShotFolderAsync window id SelectSnapShotFolderPathFailed
    // Random Drawing
    | Randomize (player, pl) -> Cmd.OfAsyncImmediate.either (PlayerLib.randomize player) (Uri pl) id RandomizeFailed
    | _ -> Cmd.none


let subs model =
    Cmd.batch [
        Cmd.ofSub setupTimer
        Cmd.ofSub (PlayerLib.timeChanged model.Player)
        Cmd.ofSub (PlayerLib.playerBuffering model.Player)
    ]
