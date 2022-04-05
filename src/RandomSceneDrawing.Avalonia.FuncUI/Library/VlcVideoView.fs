namespace LibVLCSharp.Avalonia.FuncUI

open System
open System.Reactive.Subjects

open Avalonia
open Avalonia.Controls
open Avalonia.Data
open Avalonia.Platform

open Avalonia.FuncUI
open Avalonia.Controls
open Avalonia.FuncUI.DSL
open Avalonia.Layout
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Types

open FSharpPlus
open FSharp.Control.Reactive

open LibVLCSharp

open RandomSceneDrawing
open RandomSceneDrawing.AvaloniaExtensions

module MediaPlayer =

    let inline attachHandle (platformHandle: IPlatformHandle) (mediaPlayer: MediaPlayer) =
        match Environment.OSVersion.Platform with
        | PlatformID.Win32NT -> mediaPlayer.Hwnd <- platformHandle.Handle
        | PlatformID.MacOSX -> mediaPlayer.XWindow <- uint platformHandle.Handle
        | PlatformID.Unix -> mediaPlayer.NsObject <- platformHandle.Handle
        | _ -> ()

    let inline detachHandle (mediaPlayer: MediaPlayer) =
        match Environment.OSVersion.Platform with
        | PlatformID.Win32NT -> mediaPlayer.Hwnd <- IntPtr.Zero
        | PlatformID.MacOSX -> mediaPlayer.XWindow <- 0u
        | PlatformID.Unix -> mediaPlayer.NsObject <- IntPtr.Zero
        | _ -> ()

type VideoView() =
    inherit FloatingOwnerHost()

    let mediaPlayerSub = Subject<MediaPlayer option>.behavior None
    let platformHandleSub = Subject<IPlatformHandle option>.behavior None

    let attacher =
        mediaPlayerSub
        |> Observable.combineLatest platformHandleSub
        |> Observable.subscribe (function
            | Some p, Some mp -> MediaPlayer.attachHandle p mp
            | _ -> ())

    let nativePresenter =
        { new NativeControlHost(Name = "VideoView-NativePresenter") with
            override _.CreateNativeControlCore(parent) =
                base.CreateNativeControlCore parent
                |> tap (Some >> platformHandleSub.OnNext)

            override _.DestroyNativeControlCore(control) =
                attacher.Dispose()

                Option.iter MediaPlayer.detachHandle mediaPlayerSub.Value
                platformHandleSub.OnNext None

                base.DestroyNativeControlCore control }

    override x.OnInitialized() = x.VisualChildren.Add nativePresenter

    member _.MediaPlayer
        with get () =
            if mediaPlayerSub.IsDisposed then
                None
            else
                mediaPlayerSub.Value
        and set value =
            match mediaPlayerSub.Value, value with
            | Some prev, Some next when obj.ReferenceEquals(prev, next) -> ()
            | Some prev, _ ->
                MediaPlayer.detachHandle prev
                mediaPlayerSub.OnNext value
            | _ -> mediaPlayerSub.OnNext value

    static member MediaPlayerProperty =
        AvaloniaProperty.RegisterDirect<VideoView, MediaPlayer option>(
            nameof MediaPlayer,
            (fun o -> o.MediaPlayer),
            (fun o v -> o.MediaPlayer <- v),
            defaultBindingMode = BindingMode.TwoWay
        )

    member _.IsVideoVisible
        with get () = nativePresenter.IsVisible
        and set value = nativePresenter.IsVisible <- value

    static member IsVideoVisibleProperty =
        AvaloniaProperty.RegisterDirect(
            nameof Unchecked.defaultof<VideoView>.IsVideoVisible,
            (fun (o: VideoView) -> o.IsVideoVisible),
            (fun (o: VideoView) v -> o.IsVideoVisible <- v)
        )

module VideoView =
    open Avalonia.FuncUI.Builder

    let create (attrs: IAttr<VideoView> list) : IView<VideoView> = ViewBuilder.Create<VideoView>(attrs)

    let mediaPlayer<'t when 't :> VideoView> (player: MediaPlayer option) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<MediaPlayer option>(VideoView.MediaPlayerProperty, player, ValueNone)

    let isVideoVisible<'t when 't :> VideoView> (isVideoVisible: bool) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<bool>(VideoView.IsVideoVisibleProperty, isVideoVisible, ValueNone)
