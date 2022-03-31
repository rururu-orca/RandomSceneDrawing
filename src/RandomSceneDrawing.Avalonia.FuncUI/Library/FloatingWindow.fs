namespace RandomSceneDrawing

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Templates
open Avalonia.Controls.Primitives
open Avalonia.Interactivity
open Avalonia.Media
open Avalonia.Layout
open Avalonia.VisualTree
open Avalonia.Win32
open Avalonia.Threading

open Avalonia.FuncUI
open Avalonia.FuncUI.DSL
open Avalonia.FuncUI.Types
open Avalonia.FuncUI.Builder

open System
open FSharpPlus
open FSharp.Control
open FSharp.Control.Reactive

open RandomSceneDrawing.AvaloniaExtensions
open RandomSceneDrawing.NativeModule

open FluentAvalonia.Styling

type FloatingWindowImpl() =
    inherit WindowImpl()

    static let ownerList = MailboxProcessor.createAgent Map.empty

    let tryGetOwner (x: FloatingWindowImpl) =
        Map.tryFind x.Handle.Handle
        |> MailboxProcessor.postAndReply ownerList

    let (|OwnerHandle|_|) (x: FloatingWindowImpl) =
        tryGetOwner x |> Option.map WindowBase.getHandle

    let (|NotEq|_|) x target =
        if target <> x then
            Some target
        else
            None

    static member Register (floatingWindow: WindowBase) owner =
        Map.add floatingWindow.PlatformImpl.Handle.Handle owner
        |> MailboxProcessor.post ownerList

    override x.WndProc(hWnd, msg, wParam, lParam) =
        match msg, wParam, x with
        | WM_NCACTIVATE, Active, OwnerHandle owner ->
            PostMessage(owner, msg, nativeBool true, 0)
            |> ignore

            ``base``.WndProc(hWnd, msg, wParam, lParam)

        | WM_NCACTIVATE, Deactive, OwnerHandle (NotEq lParam owner) ->

            PostMessage(owner, msg, wParam, lParam) |> ignore

            ``base``.WndProc(hWnd, msg, wParam, lParam)
        | other -> ``base``.WndProc(hWnd, msg, wParam, lParam)

open System.Diagnostics
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
type FloatingWindow([<Optional; DefaultParameterValue("")>] floatingWindowName:string) =
    inherit Window
        (
            new FloatingWindowImpl(),
            SystemDecorations = SystemDecorations.None,
            TransparencyLevelHint = WindowTransparencyLevel.Transparent,
            Background = Brushes.Transparent,
            TransparencyBackgroundFallback = Brushes.Black,
            SizeToContent = SizeToContent.Manual,
            ShowInTaskbar = false
        )



    static let floatingList = MailboxProcessor.createAgent Map.empty

    let mutable owner: IVisual = base.VisualRoot

    let getVisualRoot (visual: IVisual) = visual.VisualRoot :?> WindowBase


    static member TryGet name =
        Map.tryFind name
        |> MailboxProcessor.postAndReply floatingList

    member x.Owner
        with get (): IVisual = owner
        and set (value) =
            getVisualRoot value
            |> FloatingWindowImpl.Register x

            owner <- value

    member _.FloatingWindowName = floatingWindowName

    member x.RaizeOwnerEvent e =
        match x.Owner with
        | :? IInteractive as i -> i.RaiseEvent e
        | _ -> ()


    override this.OnInitialized() =
        if not <| String.IsNullOrEmpty floatingWindowName then
            Map.add floatingWindowName this
            |> MailboxProcessor.post floatingList

        this.PointerPressed
        |> Observable.add (fun e ->
            match this.Content with
            | :? IControl as c when not c.IsPointerOver && this.IsPointerOver -> this.RaizeOwnerEvent e
            | _ -> ())

        this.PointerReleased
        |> Observable.add (fun e ->
            match this.Content with
            | :? IControl as c when not c.IsPointerOver && this.IsPointerOver -> this.RaizeOwnerEvent e
            | _ -> ())

        this.GetPropertyChangedObservable WindowBase.ContentProperty
        |> Observable.add (fun e ->
            match e.NewValue with
            | :? IView as v -> this.Content <- VirtualDom.VirtualDom.create v
            | _ -> ())

#if DEBUG
        this.AttachDevTools()
#endif

type FloatingWindowOwnerImpl() =
    inherit WindowImpl()

    let getOwnerHandle (f: FloatingWindow) =
        f.Owner.VisualRoot :?> WindowBase
        |> WindowBase.getHandle

    let isToClientFloating (window: WindowBase) handle (ownerImpl: WindowImpl) =
        match window with
        | :? FloatingWindow as f ->
            WindowBase.getHandle f = handle
            && getOwnerHandle f = ownerImpl.Handle.Handle
        | _ -> false

    let (|ToClientFloating|_|) (ownerImpl: WindowImpl, handle) =
        getCurrentWindows ()
        |> Seq.tryPick (function
            | f when isToClientFloating f handle ownerImpl -> Some ToClientFloating
            | _ -> None)

    override x.WndProc(hWnd, msg, wParam, lParam) =

        match msg, wParam, (x, lParam) with
        | WM_NCACTIVATE, Deactive, ToClientFloating -> ``base``.WndProc(hWnd, msg, nativeBool true, 0)
        | other -> ``base``.WndProc(hWnd, msg, wParam, lParam)


type FloatingOwner() =
    inherit ContentControl()
    let mutable floatingWindow = FloatingWindow()

    member x.FloatingWindow
        with get () = floatingWindow
        and set (value: FloatingWindow) =
            floatingWindow.Close()
            value.Content <- floatingWindow.Content
            floatingWindow <- value

    static member FloatingWindowProperty =
        AvaloniaProperty.RegisterDirect<FloatingOwner,_>(
            nameof Unchecked.defaultof<FloatingOwner>.FloatingWindow,
            (fun o -> o.FloatingWindow),
            (fun o v -> o.FloatingWindow <- v)
        )
module FloatingOwner =
    let floatingWindow<'t when 't :> FloatingOwner> (floatingWindow: FloatingWindow) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<FloatingWindow>(FloatingOwner.FloatingWindowProperty, floatingWindow, ValueNone)

module FloatingContent =

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

    let fitWindowPosition (floating: FloatingWindow) (owner: ContentControl) =
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

    let getFloating (o: FloatingOwner) = o.FloatingWindow

    let showAtMe control : IDisposable =
        let disposables = Disposable.Composite
        let root = (control :> IVisual).VisualRoot :?> Window
        let floating = getFloating control

        let bindToControl (property: 'T) =
            bindProperty<'T> disposables property control

        let subscribeForUpdatelayout (observable: IObservable<'T>) =
            Observable.skip 1 observable
            |> Observable.subscribe (fun _ -> fitWindowPosition floating control)
            |> Disposable.disposeWith disposables

        floating.Owner <- control

        floating
        |> bindToControl ContentControl.ContentProperty

        control.GetObservable ContentControl.BoundsProperty
        |> subscribeForUpdatelayout

        root.PositionChanged |> subscribeForUpdatelayout

        root.GetObservable Window.WindowStateProperty
        |> Observable.filter ((<>) WindowState.Minimized)
        |> subscribeForUpdatelayout

        floating.PlatformImpl.SetParent root.PlatformImpl
        floating.Show root

        { new IDisposable with
            member x.Dispose() = floating.Close() }
        |> Disposable.disposeWith disposables

        task {
            do! System.Threading.Tasks.Task.Delay 1
            fitWindowPosition floating control
        }
        |> ignore

        disposables


type SubWindow() =
    inherit ContentControl()

    static let undef = Unchecked.defaultof<SubWindow>

    let floatingDisposables = Disposable.Composite

    let floating =

        Window(
            SystemDecorations = SystemDecorations.Full,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            ShowInTaskbar = false,
            Title = "Tool"
        )
        |> tap (fun w ->
            AvaloniaLocator
                .Current
                .GetService<FluentAvaloniaTheme>()
                .ForceWin32WindowToTheme w
#if DEBUG
            w.AttachDevTools()
#endif
        )


    static member FloatingWindow() = ()

    member _.WindowOpacity
        with get () = floating.Opacity
        and set newValue = floating.Opacity <- newValue

    static member WindowOpacityProperty =
        AvaloniaProperty.RegisterDirect(
            nameof undef.WindowOpacity,
            (fun (o: SubWindow) -> o.WindowOpacity),
            (fun (o: SubWindow) v -> o.WindowOpacity <- v)
        )

    override x.OnAttachedToVisualTree e =
        let rootWindow = (x :> IVisual).VisualRoot :?> Window

        let bindToContent (property: 'T) =
            bindProperty<'T> floatingDisposables property x floating

        let bindToWindow (property: 'T) =
            bindProperty<'T> floatingDisposables property rootWindow floating

        bindToContent Control.MarginProperty
        bindToContent ContentControl.ContentProperty
        bindToContent ContentControl.BackgroundProperty

        x.GetObservable ContentControl.IsVisibleProperty
        |> Observable.subscribe (function
            | true -> floating.Show rootWindow
            | false -> floating.Hide())
        |> floatingDisposables.Add

        bindToWindow Window.BackgroundProperty
        bindToWindow Window.TransparencyBackgroundFallbackProperty

        base.OnAttachedToVisualTree e

module SubWindow =
    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.Types

    let create (attrs: IAttr<SubWindow> list) : IView<SubWindow> = ViewBuilder.Create<SubWindow>(attrs)

    let windowOpacity<'t when 't :> SubWindow> (opacity: float) : IAttr<'t> =
        AttrBuilder<'t>
            .CreateProperty<float>(SubWindow.WindowOpacityProperty, opacity, ValueNone)
