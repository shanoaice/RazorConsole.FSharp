module RazorConsole.Elmish.Test.MvuComponentBaseTests

open System.Threading.Tasks
open Xunit
open Swensen.Unquote
open Microsoft.AspNetCore.Components.RenderTree
open RazorConsole.FSharp

type SimpleModel = { Count: int; Title: string }

type SimpleMsg =
    | Increment
    | SetTitle of string

type DirectMvuComponent(initialCount: int) =
    inherit MvuComponentBase<SimpleModel, SimpleMsg>()

    let updateCount = ref 0
    member _.UpdateCount = !updateCount

    override this.OnInitialized() =
        let initModel = { Count = initialCount; Title = "Counter" }
        let view m =
            Node(fun _ builder seq ->
                builder.AddContent(seq, $"{m.Title}: {m.Count}")
                seq + 1)

        this.SetInitialState(
            initModel,
            view initModel,
            fun msg ->
                match msg with
                | Increment ->
                    incr updateCount
                    let newModel = { this.CurrentModel.Value with Count = this.CurrentModel.Value.Count + 1 }
                    this.UpdateState(newModel, view)
                | SetTitle newTitle ->
                    let newModel = { this.CurrentModel.Value with Title = newTitle }
                    this.UpdateState(newModel, view)
        )

type CustomEqualityMvuComponent(initialCount: int) =
    inherit MvuComponentBase<SimpleModel, SimpleMsg>()

    let viewCallCount = ref 0
    member _.ViewCallCount = !viewCallCount

    // Only render when Count changes, ignore Title changes
    override _.ShouldRender(oldModel, newModel) =
        oldModel.Count <> newModel.Count

    override this.OnInitialized() =
        let initModel = { Count = initialCount; Title = "Init" }
        let view m =
            incr viewCallCount
            Node(fun _ builder seq ->
                builder.AddContent(seq, $"Count: {m.Count}")
                seq + 1)

        this.SetInitialState(
            initModel,
            view initModel,
            fun msg ->
                match msg with
                | Increment ->
                    let newModel = { this.CurrentModel.Value with Count = this.CurrentModel.Value.Count + 1 }
                    this.UpdateState(newModel, view)
                | SetTitle t ->
                    let newModel = { this.CurrentModel.Value with Title = t }
                    this.UpdateState(newModel, view)
        )

let private firstText (frames: ArrayRange<RenderTreeFrame>) =
    if frames.Count = 0 then null
    else frames.Array[0].TextContent

[<Fact>]
let ``MvuComponentBase initializes and renders initial view`` () = task {
    use renderer = new TestRenderer()
    let comp = DirectMvuComponent(5)
    let! compId = renderer.MountComponentAsync(comp)

    let frames = renderer.GetFrames(compId)
    let frameCount = frames.Count
    let text = firstText frames
    let currentModel = comp.CurrentModel

    test <@ frameCount = 1 @>
    test <@ text = "Counter: 5" @>
    test <@ currentModel = Some { Count = 5; Title = "Counter" } @>
}

[<Fact>]
let ``Dispatch updates state and triggers re-render`` () = task {
    use renderer = new TestRenderer()
    let comp = DirectMvuComponent(10)
    let! compId = renderer.MountComponentAsync(comp)

    comp.Dispatch(Increment)
    do! renderer.WaitForQuiescenceAsync()

    let frames = renderer.GetFrames(compId)
    let text = firstText frames
    let currentModel = comp.CurrentModel
    let updates = comp.UpdateCount

    test <@ text = "Counter: 11" @>
    test <@ currentModel = Some { Count = 11; Title = "Counter" } @>
    test <@ updates = 1 @>
}

[<Fact>]
let ``ShouldRender suppression prevents view re-evaluation and re-rendering`` () = task {
    use renderer = new TestRenderer()
    let comp = CustomEqualityMvuComponent(0)
    let! compId = renderer.MountComponentAsync(comp)

    let initialCalls = comp.ViewCallCount
    test <@ initialCalls = 1 @>

    // Dispatch message that changes Title only
    comp.Dispatch(SetTitle "IgnoredTitle")
    do! renderer.WaitForQuiescenceAsync()

    // Model changed, but view should not re-evaluate
    let titleAfterIgnored = comp.CurrentModel.Value.Title
    let callsAfterIgnored = comp.ViewCallCount
    test <@ titleAfterIgnored = "IgnoredTitle" @>
    test <@ callsAfterIgnored = 1 @>

    // Dispatch message that changes Count
    comp.Dispatch(Increment)
    do! renderer.WaitForQuiescenceAsync()

    let countAfterIncrement = comp.CurrentModel.Value.Count
    let callsAfterIncrement = comp.ViewCallCount
    let frames = renderer.GetFrames(compId)
    let text = firstText frames

    test <@ countAfterIncrement = 1 @>
    test <@ callsAfterIncrement = 2 @>
    test <@ text = "Count: 1" @>
}
