namespace RandomSceneDrawing

module AvaloniaExtensions =
    open Avalonia
    open Avalonia.Markup.Xaml.Styling
    open System
    open Avalonia.Styling

    open Avalonia.Controls
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

    type Styles with

        member this.Load(source: string) =
            let style = StyleInclude(baseUri = null)
            style.Source <- Uri(source)
            this.Add(style)


    open Avalonia.FuncUI.Builder
    open Avalonia.FuncUI.Types

    module Panel =
        let create (attrs: IAttr<Panel> list) : IView<Panel> = ViewBuilder.Create<Panel>(attrs)
