module RazorConsole.Elmish.Test.ProgramComponentTests

open System.Threading.Tasks
open Xunit
open Swensen.Unquote
open Elmish
open Microsoft.AspNetCore.Components.RenderTree
open RazorConsole.Elmish

type CounterModel = { Count: int; Label: string }

type CounterMsg =
    | Increment
    | Decrement
    | SetCount of int
    | NoOp

type CounterComponent(initialCount: int, ?initialCmd: Cmd<CounterMsg>) =
    inherit ProgramComponent<CounterModel, CounterMsg>()

    let mutable renderCount = 0

    member _.RenderCount = renderCount

    override _.Program =
        let init _ =
            let cmd = defaultArg initialCmd Cmd.none
            { Count = initialCount; Label = "Counter" }, cmd

        let update msg model =
            match msg with
            | Increment -> { model with Count = model.Count + 1 }, Cmd.none
            | Decrement -> { model with Count = model.Count - 1 }, Cmd.none
            | SetCount n -> { model with Count = n }, Cmd.none
            | NoOp -> model, Cmd.none

        let view model dispatch =
            renderCount <- renderCount + 1
            Node(fun receiver builder seq ->
                builder.AddContent(seq, $"{model.Label}: {model.Count}")
                seq + 1)

        Program.mkProgram init update view

type CustomEqualityComponent(initialCount: int) =
    inherit ProgramComponent<CounterModel, CounterMsg>()

    let mutable viewEvaluationCount = 0
    member _.ViewEvaluationCount = viewEvaluationCount

    // Only re-render when Count changes; ignore Label changes
    override _.ShouldRender(oldModel, newModel) =
        oldModel.Count <> newModel.Count

    override _.Program =
        let init _ = { Count = initialCount; Label = "Init" }, Cmd.none
        let update msg model =
            match msg with
            | Increment -> { model with Count = model.Count + 1 }, Cmd.none
            | NoOp -> { model with Label = "UpdatedLabel" }, Cmd.none
            | _ -> model, Cmd.none

        let view model dispatch =
            viewEvaluationCount <- viewEvaluationCount + 1
            Node(fun receiver builder seq ->
                builder.AddContent(seq, $"Count: {model.Count}")
                seq + 1)

        Program.mkProgram init update view

let private firstText (frames: ArrayRange<RenderTreeFrame>) =
    if frames.Count = 0 then null
    else frames.Array[0].TextContent

[<Fact>]
let ``Mounting component evaluates init and renders initial view`` () = task {
    use renderer = new TestRenderer()
    let comp = CounterComponent(42)
    let! compId = renderer.MountComponentAsync(comp)

    let frames = renderer.GetFrames(compId)
    let count = frames.Count
    let text = firstText frames
    let frameType = if count > 0 then frames.Array[0].FrameType else RenderTreeFrameType.None
    let renderCount = comp.RenderCount

    test <@ count = 1 @>
    test <@ frameType = RenderTreeFrameType.Text @>
    test <@ text = "Counter: 42" @>
    test <@ renderCount = 1 @>
}

[<Fact>]
let ``Initial command returned from init executes on first render`` () = task {
    use renderer = new TestRenderer()
    let initialCmd = Cmd.ofMsg (SetCount 99)
    let comp = CounterComponent(0, initialCmd)
    let! compId = renderer.MountComponentAsync(comp)

    do! renderer.WaitForQuiescenceAsync()

    let frames = renderer.GetFrames(compId)
    let text = firstText frames
    test <@ text = "Counter: 99" @>
}

[<Fact>]
let ``Dispatching message updates state and re-renders view`` () = task {
    use renderer = new TestRenderer()
    let comp = CounterComponent(10)
    let! compId = renderer.MountComponentAsync(comp)

    comp.Dispatch(Increment)
    do! renderer.WaitForQuiescenceAsync()

    let text1 = firstText (renderer.GetFrames(compId))
    test <@ text1 = "Counter: 11" @>

    comp.Dispatch(Decrement)
    do! renderer.WaitForQuiescenceAsync()

    let text2 = firstText (renderer.GetFrames(compId))
    test <@ text2 = "Counter: 10" @>
}

[<Fact>]
let ``Default ShouldRender skips re-render when model reference is unchanged`` () = task {
    use renderer = new TestRenderer()
    let comp = CounterComponent(5)
    let! compId = renderer.MountComponentAsync(comp)

    let initialRenderCount = comp.RenderCount

    comp.Dispatch(NoOp)
    do! renderer.WaitForQuiescenceAsync()

    let afterRenderCount = comp.RenderCount
    test <@ afterRenderCount = initialRenderCount @>
}

[<Fact>]
let ``Overridden ShouldRender respects custom model comparison predicate`` () = task {
    use renderer = new TestRenderer()
    let comp = CustomEqualityComponent(100)
    let! compId = renderer.MountComponentAsync(comp)

    let initialEvalCount = comp.ViewEvaluationCount
    test <@ initialEvalCount = 1 @>

    // NoOp updates Label but not Count -> ShouldRender returns false
    comp.Dispatch(NoOp)
    do! renderer.WaitForQuiescenceAsync()
    let noOpEvalCount = comp.ViewEvaluationCount
    test <@ noOpEvalCount = 1 @>

    // Increment updates Count -> ShouldRender returns true
    comp.Dispatch(Increment)
    do! renderer.WaitForQuiescenceAsync()
    let incEvalCount = comp.ViewEvaluationCount
    test <@ incEvalCount = 2 @>

    let text = firstText (renderer.GetFrames(compId))
    test <@ text = "Count: 101" @>
}

[<Fact>]
let ``Concurrent dispatches from multiple background threads are synchronized`` () = task {
    use renderer = new TestRenderer()
    let comp = CounterComponent(0)
    let! compId = renderer.MountComponentAsync(comp)

    let totalDispatches = 50
    let tasks =
        [| for _ in 1..totalDispatches ->
            Task.Run(fun () -> comp.Dispatch(Increment)) |]

    do! Task.WhenAll(tasks)
    do! renderer.WaitForQuiescenceAsync()

    let text = firstText (renderer.GetFrames(compId))
    let expected = $"Counter: {totalDispatches}"
    test <@ text = expected @>
}
