# View Combinator Testing Guide: In-Memory Render Tree Verification

## 1. Executive Summary

This guide explains how `RazorConsole.Elmish` tests declarative view combinators.
The test suite validates `RenderTreeBuilder` emission without starting a real terminal.
It uses an in-memory Blazor renderer named `TestRenderer`.
The tests execute in milliseconds and produce deterministic results.

---

## 2. In-Memory Rendering Architecture

Testing terminal UI directly can be slow and brittle.
RazorConsole avoids terminal screen scraping during unit tests.
Instead, it verifies the linear diff stream produced by Microsoft Blazor's render tree engine.

```
+--------------------------------------------------------------------------------+
|                         Test Harness Architecture                              |
|                                                                                |
|    +-----------------------------+           +-----------------------------+   |
|    |      Test Component         |           |       TestRenderer          |   |
|    |  (ViewTestComponent / Node) |           |  (inherits Blazor Renderer) |   |
|    +--------------+--------------+           +--------------+--------------+   |
|                   |                                         |                  |
|                   | BuildRenderTree(builder)                |                  |
|                   +---------------------------------------->+                  |
|                                                             |                  |
|                                                             | Emits            |
|                                                             v                  |
|                                              ArrayRange<RenderTreeFrame>       |
|                                              [0] Element: <div>                |
|                                              [1] Attribute: class="panel"      |
|                                              [2] Attribute: data-header="Log"  |
|                                              [3] Text: "Message"               |
+--------------------------------------------------------------------------------+
```

### 2.1 The `TestRenderer` Harness
[TestRenderer.fs](file:///home/shanoaice/Projects/github/RazorConsole.Elmish/test/TestRenderer.fs) provides a headless test host:

* **Inherits `Renderer`:** It implements Microsoft ASP.NET Core's low-level `Renderer` class.
* **Captures Frame Buffers:** It calls `this.GetCurrentRenderTreeFrames(componentId)`.
* **Single-Threaded Dispatcher:** It instantiates `Dispatcher.CreateDefault()`.
* **Quiescence Synchronization:** `WaitForQuiescenceAsync` awaits pending dispatcher tasks.

---

## 3. The Structure of `RenderTreeFrame`

Blazor represents UI trees as a flat array of value-type `RenderTreeFrame` structs.
This design avoids allocating thousands of individual object nodes.

### 3.1 Primary Frame Types

| Frame Type | Meaning | Key Properties to Inspect |
| :--- | :--- | :--- |
| `RenderTreeFrameType.Element` | Start of an element (`<div>`, `<button>`) | `ElementName`, `Sequence` |
| `RenderTreeFrameType.Attribute` | Attribute or parameter on the preceding element | `AttributeName`, `AttributeValue` |
| `RenderTreeFrameType.Text` | Plain text node | `TextContent` |
| `RenderTreeFrameType.Markup` | Raw markup or Spectre markup text | `MarkupContent` |
| `RenderTreeFrameType.Region` | Sequence isolation boundary | `Sequence` |
| `RenderTreeFrameType.Component` | Child Blazor component | `ComponentType`, `Component` |

---

## 4. How Each Combinator is Tested

The reference test suite lives in [ViewCombinatorTests.fs](file:///home/shanoaice/Projects/github/RazorConsole.Elmish/test/ViewCombinatorTests.fs).

### 4.1 Empty Node (`View.empty`)
```fsharp
let comp = ViewTestComponent(View.empty)
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
test <@ frames.Count = 0 @>
```
* **Assertion:** No frames are emitted into the builder.

### 4.2 Text Content (`View.text`)
```fsharp
let comp = ViewTestComponent(View.text "Hello Console")
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
test <@ frames.Count = 1 @>
test <@ frames.Array[0].FrameType = RenderTreeFrameType.Text @>
test <@ frames.Array[0].TextContent = "Hello Console" @>
```
* **Assertion:** Emits exactly one `Text` frame containing the string.

### 4.3 Spectre Markup (`View.markup`)
```fsharp
let comp = ViewTestComponent(View.markup "[bold green]Success[/]")
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
test <@ frames.Array[0].FrameType = RenderTreeFrameType.Markup @>
test <@ frames.Array[0].MarkupContent = "[bold green]Success[/]" @>
```
* **Assertion:** Emits a `Markup` frame preserving Spectre formatting tags.

### 4.4 Panel Layout (`View.panel`)
```fsharp
let node = View.panel "My Panel" "rounded" (View.text "Child")
let comp = ViewTestComponent(node)
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
```
* **Assertion 1:** Root frame is an element with name `"div"`.
* **Assertion 2:** Attributes contain `class="panel"`, `data-header="My Panel"`, and `data-border="rounded"`.
* **Assertion 3:** The child node executes after attributes are written.

### 4.5 Container Layouts (`View.rows` and `View.columns`)
```fsharp
let node = View.rows [ View.text "Row 1"; View.text "Row 2" ]
let comp = ViewTestComponent(node)
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
```
* **Assertion 1:** Emits a `<div>` element.
* **Assertion 2:** Emits `class="rows"` (or `class="columns"`).
* **Assertion 3:** Child nodes are emitted sequentially inside the element.

### 4.6 Dynamic Sequences and Region Isolation (`View.forEach`)
```fsharp
let items = [ "Alpha"; "Beta"; "Gamma" ]
let node = View.forEach items (fun item -> View.text item)
let comp = ViewTestComponent(node)
let! compId = renderer.MountComponentAsync(comp)
let frames = renderer.GetFrames(compId)
test <@ frames.Array[0].FrameType = RenderTreeFrameType.Region @>
```
* **Critical Requirement:** Dynamic collections must be wrapped in `builder.OpenRegion(seq) ... builder.CloseRegion()`.
* **Why it matters:** List insertions and removals change element counts. Without `OpenRegion`, sequence numbers shift. Shifted sequence numbers break Blazor's linear diffing algorithm.

### 4.7 Buttons and Event Callbacks (`View.button`)
```fsharp
let clicked = ref false
let node = View.button "Click Me" () (fun () -> clicked.Value <- true)
let comp = ViewTestComponent(node)
let! compId = renderer.MountComponentAsync(comp)

let frames = renderer.GetFrames(compId)
let onclickAttr = frames.Array |> Array.find (fun a -> a.AttributeName = "onclick")
let callback = onclickAttr.AttributeValue :?> EventCallback
do! renderer.Dispatcher.InvokeAsync(fun () -> callback.InvokeAsync(null))
test <@ !clicked = true @>
```
* **Critical Requirement:** Blazor event callbacks trigger `StateHasChanged()`.
* `StateHasChanged` asserts that code runs on the component's `Dispatcher`.
* The test invokes `callback.InvokeAsync(null)` inside `renderer.Dispatcher.InvokeAsync`.

---

## 5. TDD Progression Workflow

1. **Pick One Combinator:**
   Start with `View.text`.
2. **Run the Targeted Test:**
   ```bash
   dotnet test --filter "DisplayName~View.text"
   ```
   The test will fail with the stub exception (Red state).
3. **Implement in `src/RazorConsole.FSharp/View.fs`:**
   Write the delegate implementation in [src/RazorConsole.FSharp/View.fs](file:///home/shanoaice/Projects/github/RazorConsole.Elmish/src/RazorConsole.FSharp/View.fs).
4. **Run the Test Again:**
   Observe the green bar!
5. **Reference Check:**
   Compare your solution with [docs/reference/View.fs](file:///home/shanoaice/Projects/github/RazorConsole.Elmish/docs/reference/View.fs).
