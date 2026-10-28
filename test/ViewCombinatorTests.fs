namespace RazorConsole.Elmish.Test

open System.Threading.Tasks
open Xunit
open Swensen.Unquote
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Rendering
open Microsoft.AspNetCore.Components.RenderTree
open RazorConsole.FSharp

type ViewTestComponent(renderNode: Node) =
    inherit ComponentBase()

    override this.BuildRenderTree(builder: RenderTreeBuilder) =
        renderNode.Invoke(this, builder, 0) |> ignore

module ViewCombinatorTests =

    [<Fact>]
    let ``View.empty emits no frames`` () = task {
        use renderer = new TestRenderer()
        let comp = ViewTestComponent(View.empty)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        test <@ count = 0 @>
    }

    [<Fact>]
    let ``View.text emits text frame`` () = task {
        use renderer = new TestRenderer()
        let comp = ViewTestComponent(View.text "Hello Console")
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let frameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let textContent = if count > 0 then frames.Array[0].TextContent else ""

        test <@ count = 1 @>
        test <@ frameType = RenderTreeFrameType.Text @>
        test <@ textContent = "Hello Console" @>
    }

    [<Fact>]
    let ``View.markup emits markup frame`` () = task {
        use renderer = new TestRenderer()
        let comp = ViewTestComponent(View.markup "[bold green]Success[/]")
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let frameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let markupContent = if count > 0 then frames.Array[0].MarkupContent else ""

        test <@ count = 1 @>
        test <@ frameType = RenderTreeFrameType.Markup @>
        test <@ markupContent = "[bold green]Success[/]" @>
    }

    [<Fact>]
    let ``View.panel emits div with panel class and header`` () = task {
        use renderer = new TestRenderer()
        let node = View.panel "My Panel" "rounded" (Node(fun _ b s -> b.AddContent(s, "Inside panel"); s + 1))
        let comp = ViewTestComponent(node)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let firstFrameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let firstElementName = if count > 0 then frames.Array[0].ElementName else ""

        test <@ firstFrameType = RenderTreeFrameType.Element @>
        test <@ firstElementName = "div" @>

        let attributes =
            [ 1 .. count - 1 ]
            |> List.map (fun i -> frames.Array[i])
            |> List.filter (fun f -> f.FrameType = RenderTreeFrameType.Attribute)

        let classAttr = attributes |> List.tryFind (fun a -> a.AttributeName = "class")
        let headerAttr = attributes |> List.tryFind (fun a -> a.AttributeName = "data-header")
        let borderAttr = attributes |> List.tryFind (fun a -> a.AttributeName = "data-border")

        let classVal = classAttr |> Option.map (fun a -> string a.AttributeValue)
        let headerVal = headerAttr |> Option.map (fun a -> string a.AttributeValue)
        let borderVal = borderAttr |> Option.map (fun a -> string a.AttributeValue)

        test <@ classVal = Some "panel" @>
        test <@ headerVal = Some "My Panel" @>
        test <@ borderVal = Some "rounded" @>
    }

    [<Fact>]
    let ``View.rows emits div with rows class and multiple children`` () = task {
        use renderer = new TestRenderer()
        let node =
            View.rows [
                Node(fun _ b s -> b.AddContent(s, "Row 1"); s + 1)
                Node(fun _ b s -> b.AddContent(s, "Row 2"); s + 1)
            ]
        let comp = ViewTestComponent(node)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let firstFrameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let firstElementName = if count > 0 then frames.Array[0].ElementName else ""
        let secondAttrName = if count > 1 then frames.Array[1].AttributeName else ""
        let secondAttrVal = if count > 1 then string frames.Array[1].AttributeValue else ""

        test <@ firstFrameType = RenderTreeFrameType.Element @>
        test <@ firstElementName = "div" @>
        test <@ secondAttrName = "class" @>
        test <@ secondAttrVal = "rows" @>
    }

    [<Fact>]
    let ``View.columns emits div with columns class`` () = task {
        use renderer = new TestRenderer()
        let node =
            View.columns [
                Node(fun _ b s -> b.AddContent(s, "Col A"); s + 1)
                Node(fun _ b s -> b.AddContent(s, "Col B"); s + 1)
            ]
        let comp = ViewTestComponent(node)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let firstFrameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let firstElementName = if count > 0 then frames.Array[0].ElementName else ""
        let secondAttrName = if count > 1 then frames.Array[1].AttributeName else ""
        let secondAttrVal = if count > 1 then string frames.Array[1].AttributeValue else ""

        test <@ firstFrameType = RenderTreeFrameType.Element @>
        test <@ firstElementName = "div" @>
        test <@ secondAttrName = "class" @>
        test <@ secondAttrVal = "columns" @>
    }

    [<Fact>]
    let ``View.forEach isolates items inside a Region`` () = task {
        use renderer = new TestRenderer()
        let items = [ "Alpha"; "Beta"; "Gamma" ]
        let node = View.forEach items (fun item -> Node(fun _ b s -> b.AddContent(s, item); s + 1))
        let comp = ViewTestComponent(node)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let firstFrameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        test <@ firstFrameType = RenderTreeFrameType.Region @>
    }

    [<Fact>]
    let ``View.button binds onclick callback`` () = task {
        use renderer = new TestRenderer()
        let clicked = ref false
        let node = View.button "Click Me" () (fun () -> clicked.Value <- true)
        let comp = ViewTestComponent(node)
        let! compId = renderer.MountComponentAsync(comp)

        let frames = renderer.GetFrames(compId)
        let count = frames.Count
        let firstFrameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
        let firstElementName = if count > 0 then frames.Array[0].ElementName else ""

        test <@ firstFrameType = RenderTreeFrameType.Element @>
        test <@ firstElementName = "button" @>

        let onclickAttr =
            [ 1 .. count - 1 ]
            |> List.map (fun i -> frames.Array[i])
            |> List.tryFind (fun a -> a.FrameType = RenderTreeFrameType.Attribute && a.AttributeName = "onclick")

        let hasOnclick = onclickAttr.IsSome
        test <@ hasOnclick = true @>
        let callback = onclickAttr.Value.AttributeValue :?> EventCallback
        do! renderer.Dispatcher.InvokeAsync(fun () -> callback.InvokeAsync(null))
        let wasClicked = !clicked
        test <@ wasClicked = true @>
    }
