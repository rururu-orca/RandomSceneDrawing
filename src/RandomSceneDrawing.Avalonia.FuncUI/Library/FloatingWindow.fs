namespace LibVLCSharp.Avalonia.FuncUI

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Templates
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
open FluentAvalonia.Styling

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
