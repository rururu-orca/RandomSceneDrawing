module RandomSceneDrawing.Platform

open System
open System.IO
open System.Windows.Threading
open FSharp.Control
open Elmish
open Windows.Foundation
open Windows.UI.Popups
open Windows.Storage.Pickers
open WinRT.Interop
open RandomSceneDrawing.Types


type AsyncBuilder with
    member x.Bind(t: IAsyncOperation<'T>, f: 'T -> Async<'R>) : Async<'R> =
        async.Bind(t.AsTask() |> Async.AwaitTask, f)

let private timer =
    DispatcherTimer(DispatcherPriority.Render, Interval = TimeSpan.FromSeconds 1.0)

let setupTimer dispatch =
    timer.Tick
    |> Observable.add(fun _ -> dispatch Tick)

let startTimer onSuccess =
    async{
        timer.Start()
        return onSuccess
    }

let stopTimer onSuccess =
    async {
        timer.Stop()
        return onSuccess
    }

let sprintfDateTime format (datetime: DateTime) = datetime.ToString(format = format)

let sprintfNow format = DateTime.Now |> sprintfDateTime format

let ShowErrorDialog hwnd info msg =
    async {
        let dlg =
            MessageDialog(info, CancelCommandIndex = 0u)

        UICommand "Close" |> dlg.Commands.Add

        InitializeWithWindow.Initialize(dlg, hwnd)
        let! _ = dlg.ShowAsync()

        return msg
    }

let playSelectedVideo hwnd =
    async {
        let picker =
            FileOpenPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.VideosLibrary)


        InitializeWithWindow.Initialize(picker, hwnd)

        [ ".mp4"; ".mkv" ]
        |> List.iter picker.FileTypeFilter.Add

        match! picker.PickSingleFileAsync() with
        | null -> return PlayCandeled
        | file when String.IsNullOrEmpty file.Path ->
            return PlayFailed(PlayFailedException "メディアサーバーの動画を指定して再生することは出来ません。")

        | file ->
            let media =
                PlayerLib.getMediaFromUri (Uri file.Path)

            match! PlayerLib.playAsync PlaySuccess media with
            | Ok msg ->
                return
                    msg
                        { Title = media.Meta LibVLCSharp.Shared.MetadataType.Title
                          Duration = float media.Duration |> TimeSpan.FromMilliseconds }
            | Error e -> return PlayFailed e
    }

let selectPlayList hwnd =
    async {
        let picker =
            FileOpenPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.MusicLibrary)

        InitializeWithWindow.Initialize(picker, hwnd)

        picker.FileTypeFilter.Add ".xspf"

        match! picker.PickSingleFileAsync() with
        | null -> return SelectSnapShotFolderPathCandeled
        | file -> return SelectPlayListFilePathSuccess file.Path
    }

let selectSnapShotFolder hwnd =
    async {
        let picker =
            FolderPicker(ViewMode = PickerViewMode.List, SuggestedStartLocation = PickerLocationId.PicturesLibrary)

        InitializeWithWindow.Initialize(picker, hwnd)

        picker.FileTypeFilter.Add "*"

        match! picker.PickSingleFolderAsync() with
        | null -> return SelectSnapShotFolderPathCandeled
        | folder -> return SelectSnapShotFolderPathSuccess folder.Path
    }

let createCurrentSnapShotFolder root =
    let unfolder state =
        match state with
        | -1 -> None
        | _ ->
            let path =
                [| root
                   sprintfNow "yyyyMMdd"
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


let toCmd hwnd =
    function
    // Player
    | Play -> Cmd.OfAsyncImmediate.either playSelectedVideo hwnd id PlayFailed
    | Pause -> Cmd.OfAsyncImmediate.either PlayerLib.togglePauseAsync (Playing, Paused) PauseSuccess PauseFailed
    | Stop -> Cmd.OfAsyncImmediate.either PlayerLib.stopAsync StopSuccess id StopFailed

    | SelectPlayListFilePath -> Cmd.OfAsync.either selectPlayList hwnd id SelectPlayListFilePathFailed
    | SelectSnapShotFolderPath -> Cmd.OfAsync.either selectSnapShotFolder hwnd id SelectSnapShotFolderPathFailed
    // Random Drawing
    | Randomize pl -> Cmd.OfAsyncImmediate.either PlayerLib.randomize (Uri pl) id RandomizeFailed
    | StartDrawing -> Cmd.OfAsync.either startTimer StartDrawingSuccess id StartDrawingFailed
    | StopDrawing -> Cmd.OfAsync.result <| stopTimer StopDrawingSuccess
    | CreateCurrentSnapShotFolder root -> createCurrentSnapShotFolder root |> Cmd.ofMsg
    | TakeSnapshot path ->
        async {
            match PlayerLib.takeSnapshot PlayerLib.getSize 0u path with
            | Some path -> return TakeSnapshotSuccess
            | None -> return TakeSnapshotFailed(SnapShotFailedException "Snapshotに失敗しました。")
        }
        |> Cmd.OfAsyncImmediate.result
    | ShowErrorInfomation message ->
        ShowErrorDialog hwnd message ShowErrorInfomationSuccess
        |> Cmd.OfAsyncImmediate.result
