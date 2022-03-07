namespace LibVLCSharp.Avalonia.FuncUI

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Primitives
open Avalonia.Media
open Avalonia.Layout
open Avalonia.VisualTree
open Avalonia.Platform
open Avalonia.Threading
open System
open System.Collections.Generic
open FSharpPlus
open FSharp.Control
open FSharp.Control.Reactive

open RandomSceneDrawing.AvaloniaExtensions
open LibVLCSharp



module FloatingContent =
    let floating =
        Window(
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = WindowTransparencyLevel.Transparent,
            Background = Brushes.Transparent,
            TransparencyBackgroundFallback = Brushes.Black,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false
        )

    let getPoint left top =
        match left, top with
        | Some x, Some y -> Point(x (), y ())
        | Some x, None -> Point(x (), 0.0)
        | None, Some y -> Point(0.0, y ())
        | None, None -> Point(0.0, 0.0)

    let getLeft (owner: ILayoutable) (target: ILayoutable) () =
        match target.HorizontalAlignment with
        | HorizontalAlignment.Right -> owner.Bounds.Width - target.Bounds.Width
        | HorizontalAlignment.Center -> (owner.Bounds.Width - target.Bounds.Width) / 2.0
        | _ -> 0.0

    let getTop (owner: ILayoutable) (target: ILayoutable) () =
        match target.VerticalAlignment with
        | VerticalAlignment.Bottom -> owner.Bounds.Height - target.Bounds.Height
        | VerticalAlignment.Center -> (owner.Bounds.Height - target.Bounds.Height) / 2.0
        | _ -> 0.0

    let fitWindowPosition (floating: Window) (owner: ContentControl) =
        floating.GetVisualDescendants()
        |> Seq.tryPick (function
            | :? VisualLayerManager as m -> Some m
            | _ -> None)
        |> Option.iter (fun manager ->
            let getLeft' = Some(getLeft owner manager)
            let getTop' = Some(getTop owner manager)

            let newSizeToContent, newWidth, newHeight, newPoint =
                match manager.HorizontalAlignment, manager.VerticalAlignment with
                | (HorizontalAlignment.Stretch, VerticalAlignment.Stretch) ->
                    SizeToContent.Manual, owner.Bounds.Width, owner.Bounds.Height, getPoint None None
                | (HorizontalAlignment.Stretch, _) ->
                    SizeToContent.Width, owner.Bounds.Width, Double.NaN, getPoint None getLeft'
                | (_, VerticalAlignment.Stretch) ->
                    SizeToContent.Height, Double.NaN, owner.Bounds.Height, getPoint getTop' None
                | (_, _) -> SizeToContent.Manual, Double.NaN, Double.NaN, getPoint getLeft' getTop'

            manager.MaxWidth <- owner.Bounds.Width
            manager.MaxHeight <- owner.Bounds.Height

            floating.SizeToContent <- newSizeToContent
            floating.Width <- newWidth
            floating.Height <- newHeight

            match owner.PointToScreen newPoint with
            | newPosition when newPosition <> floating.Position -> floating.Position <- newPosition
            | _ -> ())

    let showAtMe (control: ContentControl) =
        let disposables = Disposable.Composite


#if DEBUG
        floating.AttachDevTools()
#endif

        let bindToControl (property: 'T) =
            bindProperty<'T> disposables property control

        let subscribeForUpdatelayout (observable: IObservable<'T>) =
            Observable.skip 1 observable
            |> Observable.subscribe (fun _ -> fitWindowPosition floating control)
            |> Disposable.disposeWith disposables

        let root = (control :> IVisual).VisualRoot :?> Window

        floating
        |> bindToControl ContentControl.ContentProperty

        control.GetObservable ContentControl.ContentProperty
        |> subscribeForUpdatelayout

        control.GetObservable ContentControl.BoundsProperty
        |> subscribeForUpdatelayout

        root.PositionChanged |> subscribeForUpdatelayout

        floating.Show root
        fitWindowPosition floating control

        { new IDisposable with
            member x.Dispose() = floating.Close() }
        |> Disposable.disposeWith disposables

        disposables

type private VlcNativePresenter() =
    inherit NativeControlHost()

    let mutable platformHandle = Option<IPlatformHandle>.None

    override x.CreateNativeControlCore(parent) =
        base.CreateNativeControlCore parent
        |> tap (fun handle -> platformHandle <- Some handle)

    override x.DestroyNativeControlCore(control) =
        platformHandle <- None
        base.DestroyNativeControlCore control

    member x.AttachHandle(mediaPlayer: MediaPlayer) =
        match Environment.OSVersion.Platform, platformHandle with
        | PlatformID.Win32NT, Some handle -> mediaPlayer.Hwnd <- handle.Handle
        | PlatformID.MacOSX, Some handle -> mediaPlayer.XWindow <- uint handle.Handle
        | PlatformID.Unix, Some handle -> mediaPlayer.NsObject <- handle.Handle
        | _ -> ()

    member x.DetachHandle(mediaPlayer: MediaPlayer) =
        mediaPlayer.Stop() |> ignore

        match Environment.OSVersion.Platform, platformHandle with
        | PlatformID.Win32NT, Some _ -> mediaPlayer.Hwnd <- IntPtr.Zero
        | PlatformID.MacOSX, Some _ -> mediaPlayer.XWindow <- 0u
        | PlatformID.Unix, Some _ -> mediaPlayer.NsObject <- IntPtr.Zero
        | _ -> ()

type VideoView() as x =
    inherit ContentControl()
    let floatingDisposables = Disposable.Composite
    let mediaDisposables = Disposable.Composite
    let mediaPlayerDisposables = Disposable.Composite

    let mutable nativePresenter = Option<VlcNativePresenter>.None
    let mutable mediaPlayer = Option<MediaPlayer>.None

    do x.Styles.Load "avares://RandomSceneDrawing.Avalonia.FuncUI/Library/VlcVideoViewStyles.xaml"

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


    member val HasFloating = false with get, set

    static member HasFloatingProperty =
        AvaloniaProperty.RegisterDirect(
            nameof Unchecked.defaultof<VideoView>.HasFloating,
            (fun (o: VideoView) -> o.HasFloating),
            (fun (o: VideoView) v -> o.HasFloating <- v)
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


    static member VideoPanel() =
        [ VideoView.MediaPlayerProperty.Changed
          |> addClassHandler<VideoView, MediaPlayer> (fun s e -> s.InitMediaPlayer()) ]
        |> Disposables.compose

    override x.OnApplyTemplate e =
        base.OnApplyTemplate e
        nativePresenter <- findNameScope<VlcNativePresenter> "nativePresenter" e.NameScope

        x.InitMediaPlayer()

    member private x.InitMediaPlayer() =
        match mediaPlayer, nativePresenter with
        | Some player, Some presenter ->
            presenter.AttachHandle player
            presenter.IsVisible <- false
        | _ -> ()

    override x.Render context =
        if x.HasFloating && floatingDisposables.Count = 0 then
            FloatingContent.showAtMe x
            |> Disposable.disposeWith floatingDisposables

        base.Render context

    override x.OnDetachedFromVisualTree e =
        match nativePresenter, mediaPlayer with
        | Some nativePresenter', Some mediaPlayer' ->
            nativePresenter'.DetachHandle mediaPlayer'
            mediaPlayer <- None
        | _ -> ()

        mediaDisposables.Dispose()
        mediaPlayerDisposables.Dispose()

        floatingDisposables.Dispose()

        base.OnDetachedFromVisualTree e

module VideoView =
    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.Types

    let create (attrs: IAttr<VideoView> list) : IView<VideoView> = ViewBuilder.Create<VideoView>(attrs)

    let mediaPlayer<'t when 't :> VideoView> (player: MediaPlayer) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<MediaPlayer>(VideoView.MediaPlayerProperty, player, ValueNone)

    let hasFloating<'t when 't :> VideoView> (hasFloating: bool) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<bool>(VideoView.HasFloatingProperty, hasFloating, ValueNone)

    let isVideoVisible<'t when 't :> VideoView> (isVideoVisible: bool) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<bool>(VideoView.IsVideoVisibleProperty, isVideoVisible, ValueNone)

    let opacity<'t when 't :> VideoView> (opacity: float) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<float>(VideoView.OpacityProperty, opacity, ValueNone)
