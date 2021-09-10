module RandomSceneDrawing.Platform

open System
open FSharpPlus
open LibVLCSharp.Shared
open LibVLCSharp.Avalonia.FuncUI

// Define a function to construct a message to print
open Avalonia.FuncUI.DSL

open Elmish
open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Themes.Fluent
open Avalonia.FuncUI
open Avalonia.FuncUI.Elmish
open Avalonia.FuncUI.Components.Hosts
open Avalonia.Media
open RandomSceneDrawing.Types

let toCmd =
    function
    | Play player ->
        async {
            let media =
                PlayerLib.getMediaFromUri (
                    Uri "http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/ElephantsDream.mp4"
                )

            match! PlayerLib.playAsync player PlaySuccess media with
            | Ok msg ->
                return
                    msg
                        { Title = media.Meta LibVLCSharp.Shared.MetadataType.Title
                          Duration = float media.Duration |> TimeSpan.FromMilliseconds }
            | Error e -> return PlayFailed e
        }
        |> Cmd.OfAsync.result
    | Pause player ->
        Cmd.OfAsyncImmediate.either (PlayerLib.togglePauseAsync player) (Playing, Paused) PauseSuccess PauseFailed
    | Stop player -> Cmd.OfAsyncImmediate.either (PlayerLib.stopAsync player) StopSuccess id StopFailed
    | _ -> Cmd.none


let subs model =
    Cmd.batch [
        Cmd.ofSub (PlayerLib.timeChanged model.Player)
        Cmd.ofSub (PlayerLib.playerBuffering model.Player)
    ]
