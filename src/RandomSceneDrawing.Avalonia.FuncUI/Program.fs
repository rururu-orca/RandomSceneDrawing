namespace RandomSceneDrawing

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


module ProgramUtil =
    let mkProgramWithCmdMsg
        (init: unit -> 'model * 'cmdMsg list)
        (update: 'msg -> 'model -> 'model * 'cmdMsg list)
        (view: 'model -> Dispatch<'msg> -> 'view)
        (toCmd: 'cmdMsg -> Cmd<'msg>)
        =
        let convert (model, cmdMsgs) =
            model, (cmdMsgs |> List.map toCmd |> Cmd.batch)

        Program.mkProgram (init >> convert) (fun msg model -> update msg model |> convert) view


type MainWindow() as this =
    inherit HostWindow()

    do
        base.Title <- "Counter Example"
        base.Height <- 400.0
        base.Width <- 400.0

        Core.Initialize()

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
                Cmd.OfAsyncImmediate.either
                    (PlayerLib.togglePauseAsync player)
                    (Playing, Paused)
                    PauseSuccess
                    PauseFailed
            | Stop player -> Cmd.OfAsyncImmediate.either (PlayerLib.stopAsync player) StopSuccess id StopFailed
            | _ -> Cmd.none


#if DEBUG
        this.AttachDevTools()
#endif

        // this.VisualRoot.VisualRoot.Renderer.DrawFps <- true
        //this.VisualRoot.VisualRoot.Renderer.DrawDirtyRects <- true
        ProgramUtil.mkProgramWithCmdMsg Program.init Program.update MainView.view toCmd
        |> Program.withHost this
        |> Program.withSubscription
            (fun m ->
                Cmd.batch [
                    Cmd.ofSub (PlayerLib.timeChanged m.Player)
                    Cmd.ofSub (PlayerLib.playerBuffering m.Player)
                ])
        |> Program.withConsoleTrace
        |> Program.run

type App() =
    inherit Application()

    override this.Initialize() =
        this.Styles.Add(FluentTheme(baseUri = null, Mode = FluentThemeMode.Dark))

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktopLifetime ->
            let mainWindow = MainWindow()
            desktopLifetime.MainWindow <- mainWindow
        | _ -> ()

module Program =

    [<EntryPoint>]
    let main (args: string []) =
        AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .UseSkia()
            .StartWithClassicDesktopLifetime(args)
