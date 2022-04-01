namespace LibVLCSharp.Avalonia.FuncUI

open System

open Avalonia
open Avalonia.Controls
open Avalonia.Platform

open Avalonia.FuncUI.Types

open FSharpPlus
open FSharp.Control.Reactive

open LibVLCSharp

open RandomSceneDrawing
open RandomSceneDrawing.AvaloniaExtensions

type private VlcNativePresenter() =
    inherit NativeControlHost()

    let mutable platformHandle = Option<IPlatformHandle>.None

    member _.AttachHandle(mediaPlayer: MediaPlayer) =
        match Environment.OSVersion.Platform, platformHandle with
        | PlatformID.Win32NT, Some handle -> mediaPlayer.Hwnd <- handle.Handle
        | PlatformID.MacOSX, Some handle -> mediaPlayer.XWindow <- uint handle.Handle
        | PlatformID.Unix, Some handle -> mediaPlayer.NsObject <- handle.Handle
        | _ -> ()

    member _.DetachHandle(mediaPlayer: MediaPlayer) =
        mediaPlayer.Stop() |> ignore

        match Environment.OSVersion.Platform, platformHandle with
        | PlatformID.Win32NT, Some _ -> mediaPlayer.Hwnd <- IntPtr.Zero
        | PlatformID.MacOSX, Some _ -> mediaPlayer.XWindow <- 0u
        | PlatformID.Unix, Some _ -> mediaPlayer.NsObject <- IntPtr.Zero
        | _ -> ()

    override _.CreateNativeControlCore(parent) =
        base.CreateNativeControlCore parent
        |> tap (fun handle -> platformHandle <- Some handle)

    override _.DestroyNativeControlCore(control) =
        platformHandle <- None
        base.DestroyNativeControlCore control



type VideoView() =
    inherit FloatingOwner()

    let mediaPlayerDisposables = Disposable.Composite

    let mutable nativePresenter = Option<VlcNativePresenter>.None
    let mutable mediaPlayer = Option<MediaPlayer>.None

    do base.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Library/VlcVideoViewStyles.xaml"

    member private _.InitMediaPlayer() =
        match mediaPlayer, nativePresenter with
        | Some player, Some presenter ->
            presenter.AttachHandle player
        | _ -> ()

    interface IVideoView with
        member x.MediaPlayer
            with get () = Option.toObj mediaPlayer
            and set (value) =
                mediaPlayerDisposables.Clear()

                if x.SetAndRaise(VideoView.MediaPlayerProperty, (Option.toObj >> ref) mediaPlayer, value) then
                    mediaPlayer <- Option.ofObj value
                    x.InitMediaPlayer()

    static member MediaPlayerProperty =
        AvaloniaProperty.RegisterDirect(
            nameof MediaPlayer,
            (fun (o: VideoView) -> (o :> IVideoView).MediaPlayer),
            (fun (o: VideoView) v -> (o :> IVideoView).MediaPlayer <- v)
        )

    member _.IsVideoVisible
        with get () =
            nativePresenter
            |> Option.map (fun np -> np.IsVisible)
            |> Option.defaultValue false
        and set (value) =
            nativePresenter
            |> Option.iter (fun np -> np.IsVisible <- value)

    static member IsVideoVisibleProperty =
        AvaloniaProperty.RegisterDirect(
            nameof Unchecked.defaultof<VideoView>.IsVideoVisible,
            (fun (o: VideoView) -> o.IsVideoVisible),
            (fun (o: VideoView) v -> o.IsVideoVisible <- v)
        )

    static member VideoView() =
        [ VideoView.MediaPlayerProperty.Changed
          |> addClassHandler<VideoView, MediaPlayer> (fun s e -> s.InitMediaPlayer()) ]
        |> Disposables.compose

    override x.OnApplyTemplate e =

        base.OnApplyTemplate e
        nativePresenter <- findNameScope<VlcNativePresenter> "nativePresenter" e.NameScope

        x.InitMediaPlayer()

    override _.OnDetachedFromVisualTree e =
        match nativePresenter, mediaPlayer with
        | Some nativePresenter', Some mediaPlayer' ->
            nativePresenter'.DetachHandle mediaPlayer'
            mediaPlayer <- None
        | _ -> ()

        mediaPlayerDisposables.Dispose()

        base.OnDetachedFromVisualTree e

module VideoView =
    open Avalonia.FuncUI.Builder

    let create (attrs: IAttr<VideoView> list) : IView<VideoView> = ViewBuilder.Create<VideoView>(attrs)

    let mediaPlayer<'t when 't :> VideoView> (player: MediaPlayer) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<MediaPlayer>(VideoView.MediaPlayerProperty, player, ValueNone)

    let isVideoVisible<'t when 't :> VideoView> (isVideoVisible: bool) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<bool>(VideoView.IsVideoVisibleProperty, isVideoVisible, ValueNone)
