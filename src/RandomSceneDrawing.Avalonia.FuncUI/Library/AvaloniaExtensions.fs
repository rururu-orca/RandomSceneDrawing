namespace RandomSceneDrawing


open System.Runtime.InteropServices



[<AutoOpen>]
module Helper =

    let (|NotEq|_|) x target =
        if target <> x then
            Some target
        else
            None

module NativeModule =

    let CW_USEDEFAULT = ((int) 0x80000000)

    let GWL_STYLE = -16
    let GWL_EXSTYLE = -20

    let WS_EX_NOACTIVATE = 0x08000000un

    let WS_EX_TOOLWINDOW = 0x00000080un

    [<Literal>]
    let WM_ACTIVATE = 0x0006u

    [<Literal>]
    let WM_NCACTIVATE = 0x0086u

    [<Literal>]
    let SW_SHOWNOACTIVATE = 4u

    [<Literal>]
    let MA_NOACTIVATE = 3

    let WA_INACTIVE = 0

    let nativeBool (b: bool) = if b then 1n else 0n

    let (|Active|Deactive|) =
        function
        | 0n -> Deactive
        | _ -> Active

    [<DllImport("user32.dll", SetLastError = true)>]
    extern unativeint GetWindowLongPtr(nativeint hWnd, int nIndex)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern nativeint SetWindowLongPtr(nativeint hWnd, int nIndex, unativeint dwLong)

    [<DllImport("user32.dll")>]
    extern int ShowWindow(nativeint hWnd, uint nCmdShow)

    [<DllImport("user32.dll", SetLastError = true)>]
    extern nativeint CreateWindowEx(unativeint dwExStyle, uint16 lpClassName, string lpWindowName, unativeint dwStyle, int x, int y, int nWidth, int nHeight, nativeint hWndParent, nativeint hMenu, nativeint hInstance, nativeint lpParam)

    [<return: MarshalAs(UnmanagedType.Bool)>]
    [<DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)>]
    extern bool PostMessage(nativeint hWnd, uint msg, nativeint wParam, nativeint lParam)

    open Avalonia.Controls

    let addWindowExStyle addExStyle (window: WindowBase) =
        let handle = window.PlatformImpl.Handle.Handle

        let style =
            GetWindowLongPtr(handle, GWL_EXSTYLE)
            ||| addExStyle

        match SetWindowLongPtr(handle, GWL_EXSTYLE, style) with
        | 0n ->
            Marshal.GetLastPInvokeError()
            |> failwith "SetWindowLongPtr Failed: %i"
        | _ -> ()


module AvaloniaExtensions =
    open Avalonia
    open Avalonia.Markup.Xaml.Styling
    open System
    open Avalonia.Styling

    open Avalonia.Controls
    open Avalonia.Controls.Primitives
    open Avalonia.Controls.ApplicationLifetimes
    open System.Collections.Generic
    open FSharp.Control
    open FSharp.Control.Reactive

    let bindProperty<'T when 'T :> AvaloniaProperty>
        disposables
        (property: 'T)
        (source: AvaloniaObject)
        (target: AvaloniaObject)
        =
        target.Bind(property, source.GetObservable property)
        |> Disposable.disposeWith disposables

    let inline addClassHandler< ^T, ^U when ^T :> AvaloniaObject>
        ([<InlineIfLambda>] action)
        (observable: IObservable<AvaloniaPropertyChangedEventArgs< ^U >>)
        =
        observable
        |> Observable.subscribe (fun e ->
            match e.Sender with
            | :? ^T as target -> action target e
            | _ -> ())

    let findNameScope<'T when 'T: not struct> name (namescope: INameScope) =
        try
            Some(namescope.Get<'T> name)
        with
        | :? KeyNotFoundException -> None

    let inline setAvaloniaProperty (target: IAvaloniaObject) (prop: AvaloniaProperty) value =
        target.SetValue(prop, value)


    let inline getLifetime () =
        match Application.Current.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as lifetime -> Some lifetime
        | _ -> None

    let inline getCurrentWindows () =
        getLifetime ()
        |> Option.map (fun l -> l.Windows)
        |> Option.defaultValue List.Empty

    let inline optionRef o =
        (Option.toObj >> ref) o

    type Styles with

        member this.Load(source: string) =
            let style = StyleInclude(baseUri = null)
            style.Source <- Uri(source)
            this.Add(style)


    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.Types

    module Panel =
        let create (attrs: IAttr<Panel> list) : IView<Panel> = ViewBuilder.Create<Panel>(attrs)

    module OverlayLayer =
        let create (attrs: IAttr<OverlayLayer> list) : IView<OverlayLayer> = ViewBuilder.Create<OverlayLayer>(attrs)

        let getOverlayLayer visual = OverlayLayer.GetOverlayLayer visual

    module Popup =

        let create placementTarget =
            Popup(PlacementTarget = placementTarget)

    module FlyoutBase =
        let attachedFlyout<'t when 't :> Control> (value: FlyoutBase) =
            AttrBuilder<'t>.CreateProperty (FlyoutBase.AttachedFlyoutProperty, value, ValueNone)

        let showAttachedFlyout control = FlyoutBase.ShowAttachedFlyout control

    type Flyout with

        static member create(attrs: IAttr<Flyout> list) = ViewBuilder.Create<Flyout>(attrs)

        static member isOpen value =
            AttrBuilder.CreateProperty(Flyout.IsOpenProperty, value, ValueNone)

        static member placement value =
            AttrBuilder.CreateProperty(Flyout.PlacementProperty, value, ValueNone)

        static member whowMode value =
            AttrBuilder.CreateProperty(Flyout.ShowModeProperty, value, ValueNone)

        static member target value =
            AttrBuilder.CreateProperty(Flyout.TargetProperty, value, ValueNone)

    module WindowBase =
        let getHandle (w: WindowBase) = w.PlatformImpl.Handle.Handle


    type TemplatedControl with

        static member cornerRadius<'t when 't :> TemplatedControl>(value: CornerRadius) : IAttr<'t> =
            AttrBuilder<'t>.CreateProperty (TemplatedControl.CornerRadiusProperty, value, ValueNone)

        static member cornerRadius<'t when 't :> TemplatedControl>(uniformRadius) : IAttr<'t> =
            CornerRadius uniformRadius
            |> TemplatedControl.cornerRadius

        static member cornerRadius<'t when 't :> TemplatedControl>(top, bottom) : IAttr<'t> =
            CornerRadius(top, bottom)
            |> TemplatedControl.cornerRadius

        static member cornerRadius<'t when 't :> TemplatedControl>
            (
                topLeft,
                topRight,
                bottomRight,
                bottomLeft
            )
            : IAttr<'t>
            =
            CornerRadius(topLeft, topRight, bottomRight, bottomLeft)
            |> TemplatedControl.cornerRadius
