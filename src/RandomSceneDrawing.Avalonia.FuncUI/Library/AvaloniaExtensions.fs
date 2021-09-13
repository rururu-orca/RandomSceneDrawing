namespace RandomSceneDrawing

[<AutoOpen>]
module AvaloniaExtensions =
    open Avalonia.Markup.Xaml.Styling
    open System
    open Avalonia.Styling

    type Styles with
        member this.Load (source: string) = 
            let style = StyleInclude(baseUri = null)
            style.Source <- Uri(source)
            this.Add(style)

open Avalonia.Controls
open Avalonia.FuncUI.Builder
open Avalonia.FuncUI.Types
module Panel =
    let create (attrs: IAttr<Panel> list) : IView<Panel> = ViewBuilder.Create<Panel>(attrs)
