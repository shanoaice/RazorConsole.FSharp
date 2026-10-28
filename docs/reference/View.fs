namespace RazorConsole.FSharp.Reference

open System
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Rendering
open RazorConsole.FSharp

/// <summary>
/// Reference implementation of declarative view combinators producing sequence-threading <see cref="Node"/> delegates.
/// Stashed here for user study, TDD benchmarking, and reference comparison.
/// </summary>
[<RequireQualifiedAccess>]
module View =

    /// <summary>
    /// An empty node that emits nothing into the render tree.
    /// </summary>
    let empty: Node =
        Node(fun _ _ seq -> seq)

    /// <summary>
    /// Emits text content directly into the render tree.
    /// </summary>
    let text (content: string) : Node =
        Node(fun _ builder seq ->
            builder.AddContent(seq, content)
            seq + 1)

    /// <summary>
    /// Emits markup content (Spectre markup or HTML text) into the render tree.
    /// </summary>
    let markup (content: string) : Node =
        Node(fun _ builder seq ->
            builder.AddMarkupContent(seq, content)
            seq + 1)

    /// <summary>
    /// Emits an attribute name-value pair into the render tree.
    /// </summary>
    let attr (name: string) (value: obj) : Attr =
        Attr(fun _ builder seq ->
            builder.AddAttribute(seq, name, value)
            seq + 1)

    /// <summary>
    /// Emits an arbitrary element with attributes and child nodes.
    /// </summary>
    let element (tag: string) (attrs: seq<Attr>) (children: seq<Node>) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, tag)
            let mutable nextSeq = seq + 1
            for a in attrs do
                nextSeq <- a.Invoke(receiver, builder, nextSeq)
            for c in children do
                nextSeq <- c.Invoke(receiver, builder, nextSeq)
            builder.CloseElement()
            nextSeq)

    /// <summary>
    /// Emits a Spectre-styled panel container with a title and border (<c>&lt;div class="panel" data-header="..." data-border="..."&gt;</c>).
    /// </summary>
    let panel (title: string) (border: string) (child: Node) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "div")
            builder.AddAttribute(seq + 1, "class", "panel")
            builder.AddAttribute(seq + 2, "data-header", title)
            let seqAfterBorder =
                if String.IsNullOrEmpty(border) then
                    seq + 3
                else
                    builder.AddAttribute(seq + 3, "data-border", border)
                    seq + 4
            let nextSeq = child.Invoke(receiver, builder, seqAfterBorder)
            builder.CloseElement()
            nextSeq)

    /// <summary>
    /// Emits a Spectre-styled panel container with default border (<c>&lt;div class="panel" data-header="..."&gt;</c>).
    /// </summary>
    let simplePanel (title: string) (child: Node) : Node =
        panel title "" child

    /// <summary>
    /// Emits a rows layout container (<c>&lt;div class="rows"&gt;</c>).
    /// </summary>
    let rows (children: seq<Node>) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "div")
            builder.AddAttribute(seq + 1, "class", "rows")
            let mutable nextSeq = seq + 2
            for child in children do
                nextSeq <- child.Invoke(receiver, builder, nextSeq)
            builder.CloseElement()
            nextSeq)

    /// <summary>
    /// Emits a columns layout container (<c>&lt;div class="columns"&gt;</c>).
    /// </summary>
    let columns (children: seq<Node>) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "div")
            builder.AddAttribute(seq + 1, "class", "columns")
            let mutable nextSeq = seq + 2
            for child in children do
                nextSeq <- child.Invoke(receiver, builder, nextSeq)
            builder.CloseElement()
            nextSeq)

    /// <summary>
    /// Emits a button element (<c>&lt;button onclick="..."&gt;</c>) bound to dispatch a message.
    /// </summary>
    let button (content: string) (onClick: 'msg) (dispatch: 'msg -> unit) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "button")
            let callback = EventCallback.Factory.Create(receiver, Action(fun () -> dispatch onClick))
            builder.AddAttribute(seq + 1, "onclick", callback)
            builder.AddContent(seq + 2, content)
            builder.CloseElement()
            seq + 3)

    /// <summary>
    /// Emits a button element (<c>&lt;button onclick="..."&gt;</c>) bound to an action callback.
    /// </summary>
    let buttonAction (content: string) (onClick: unit -> unit) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "button")
            let callback = EventCallback.Factory.Create(receiver, Action(onClick))
            builder.AddAttribute(seq + 1, "onclick", callback)
            builder.AddContent(seq + 2, content)
            builder.CloseElement()
            seq + 3)

    /// <summary>
    /// Emits an input element (<c>&lt;input type="text" ...&gt;</c>) with a placeholder, bound to dispatch an onChanged message.
    /// </summary>
    let textInput (value: string) (placeholder: string) (onChanged: string -> 'msg) (dispatch: 'msg -> unit) : Node =
        Node(fun receiver builder seq ->
            builder.OpenElement(seq, "input")
            builder.AddAttribute(seq + 1, "type", "text")
            builder.AddAttribute(seq + 2, "value", value)
            let seqAfterPlaceholder =
                if String.IsNullOrEmpty(placeholder) then
                    seq + 3
                else
                    builder.AddAttribute(seq + 3, "placeholder", placeholder)
                    seq + 4
            let callback =
                EventCallback.Factory.Create<ChangeEventArgs>(
                    receiver,
                    Action<ChangeEventArgs>(fun args ->
                        let text = if isNull args.Value then "" else string args.Value
                        dispatch (onChanged text))
                )
            builder.AddAttribute(seqAfterPlaceholder, "onchange", callback)
            builder.CloseElement()
            seqAfterPlaceholder + 1)

    /// <summary>
    /// Emits an input element (<c>&lt;input type="text" ...&gt;</c>) without placeholder, bound to dispatch an onChanged message.
    /// </summary>
    let simpleTextInput (value: string) (onChanged: string -> 'msg) (dispatch: 'msg -> unit) : Node =
        textInput value "" onChanged dispatch

    /// <summary>
    /// Isolates a dynamic sequence inside <c>builder.OpenRegion</c> and <c>builder.CloseRegion</c>
    /// to preserve linear-time diffing when collections change size.
    /// </summary>
    let forEach (items: seq<'T>) (mkNode: 'T -> Node) : Node =
        Node(fun receiver builder seq ->
            builder.OpenRegion(seq)
            let mutable innerSeq = 0
            for item in items do
                let node = mkNode item
                innerSeq <- node.Invoke(receiver, builder, innerSeq)
            builder.CloseRegion()
            seq + 1)

    /// <summary>
    /// Escape hatch for embedding an external Blazor <see cref="RenderFragment"/> into the tree,
    /// isolated with an <c>OpenRegion</c>.
    /// </summary>
    let fragment (rf: RenderFragment) : Node =
        Node(fun _ builder seq ->
            builder.OpenRegion(seq)
            rf.Invoke(builder)
            builder.CloseRegion()
            seq + 1)

    /// <summary>
    /// Emits a Blazor component frame with attributes.
    /// </summary>
    let component'<'TComponent when 'TComponent :> IComponent> (attrs: seq<Attr>) : Node =
        Node(fun receiver builder seq ->
            builder.OpenComponent<'TComponent>(seq)
            let mutable nextSeq = seq + 1
            for attr in attrs do
                nextSeq <- attr.Invoke(receiver, builder, nextSeq)
            builder.CloseComponent()
            nextSeq)
