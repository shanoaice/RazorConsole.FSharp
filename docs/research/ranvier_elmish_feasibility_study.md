# Feasibility Study: Supporting Ranvier and Elmish in RazorConsole

## 1. Executive Summary

This study evaluates if RazorConsole can support both [Elmish](https://github.com/elmish/elmish) and [Ranvier](https://github.com/shayanhabibi/Ranvier).

**Conclusion:** Supporting both frameworks is highly feasible. 

The view engine in `RazorConsole.Elmish` uses the sequence-threading `Node` delegate.
This delegate drives `RenderTreeBuilder` directly.
The view engine does not depend on Elmish.
Therefore, the view engine can serve both Elmish and Ranvier.

Ranvier provides two integration paths:
1. **MVU Mode:** Use `Ranvier.Elmish` to run Model-View-Update on a reactive graph.
2. **Fine-Grained Reactive Mode:** Use Ranvier signals, memos, and effects with `ComponentBase`.

---

## 2. Technical Comparison

| Dimension | Elmish | Ranvier |
| :--- | :--- | :--- |
| **Paradigm** | Monolithic Model-View-Update (MVU) | Fine-grained reactive (Signals, Memos, Effects) or hybrid MVU |
| **State Container** | Single immutable model `'model` | Reactive cells: `Signal<'T>`, `Memo<'T>`, `AsyncMemo<'T>` |
| **Change Detection** | Coarse-grained `ShouldRender(oldModel, newModel)` | Fine-grained dependency tracking with equality cutoffs |
| **Side Effects** | `Cmd<'msg>` and `Sub<'msg>` | Effects (`createEffect`), async computations (`createAsync`), bindings |
| **Thread Model** | `syncDispatch` marshals pool threads to Blazor `Dispatcher` | `ThreadAffinity.Serialised` binds to Blazor `SynchronizationContext` |
| **Lifecycle** | Two-phase mount (`OnInitialized` + `OnAfterRenderAsync`) | Root scope (`createRoot`) in `OnInitialized`, disposed on `Dispose` |
| **Maturity** | Industry standard, stable, widely adopted | Modern, high-performance, active pre-release preview |

---

## 3. Core Architectural Synergies

### 3.1 Independence of the View Engine
In `src/ViewDelegates.fs`, the core delegates are:

```fsharp
type Attr = delegate of receiver: obj * builder: RenderTreeBuilder * sequence: int -> int
type Node = delegate of receiver: obj * builder: RenderTreeBuilder * sequence: int -> int
```

These delegates depend only on `Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder`.
They have no reference to `Elmish`.
Both Elmish and Ranvier components can emit terminal UI widgets using these delegates.

### 3.2 Compatibility with RazorConsole
`RazorConsole.Core` requires standard Blazor components inheriting `IComponent` or `ComponentBase`.
`ConsoleRenderer` processes `RenderBatch` diffs from `BuildRenderTree`.
It does not inspect the state management runtime.
Any component that calls `this.InvokeAsync(this.StateHasChanged)` triggers terminal screen updates.

### 3.3 Thread Synchronization Match
Blazor requires state updates on its single-threaded `Dispatcher`.
- Elmish achieves this with `syncDispatch` wrapping `this.InvokeAsync`.
- Ranvier achieves this with `ThreadAffinity.Serialised`.
Ranvier automatically posts completed async computations back to the captured `SynchronizationContext`.

---

## 4. Integration Options

### Option A: Common Core with Pluggable Adapters (Recommended)
Decouple the architecture into three layers:

```
+--------------------------------------------------------------------------+
|                              Application                                 |
|            (Chooses Elmish MVU or Ranvier Reactive State)                |
+------------------------------------+-------------------------------------+
|        RazorConsole.Elmish         |        RazorConsole.Ranvier         |
|     (ProgramComponent<'model,'msg>) |     (RanvierComponent / MVU)        |
+------------------------------------+-------------------------------------+
|                 RazorConsole.FSharp (Shared Core)                         |
|     - Node and Attr delegates                                            |
|     - Declarative View DSL (View.panel, View.rows, View.button, etc.)    |
|     - RenderTreeBuilder helpers                                          |
+--------------------------------------------------------------------------+
|                 RazorConsole.Core (ConsoleRenderer)                      |
+--------------------------------------------------------------------------+
```

1. **`RazorConsole.FSharp` (or `RazorConsole.View`):**
   Holds `Node`, `Attr`, and the terminal layout DSL (`View.panel`, `View.rows`, `View.button`).
2. **`RazorConsole.Elmish`:**
   References the shared core and `Elmish`.
   Provides `ProgramComponent<'model, 'msg>`.
3. **`RazorConsole.Ranvier`:**
   References the shared core and `Ranvier`.
   Provides `RanvierComponent` and `RanvierMvuComponent`.

### Option B: Ranvier MVU Bridge (`Ranvier.Elmish`)
Ranvier already includes `Ranvier.Elmish.Mvu`.
The command signature in `Ranvier.Elmish` matches Elmish's `Cmd`:

```fsharp
type Cmd<'msg> = (('msg -> unit) -> unit) list
```

Developers can use `Mvu.create` or `Mvu.withCmd`.
Existing Elmish `init` and `update` functions work without modification.
Selectors (`mvu.Select`) allow sub-views to observe only parts of the model.

### Option C: Fine-Grained `RanvierComponent`
For a direct reactive experience without MVU message boilerplate:

```fsharp
[<AbstractClass>]
type RanvierComponent() =
    inherit ComponentBase()

    let mutable scope: Owner option = None
    abstract Graph: Graph

    default this.Graph = Graph.Current

    abstract View: unit -> Node

    override this.OnInitialized() =
        this.Graph.Run(fun () ->
            createRoot(fun owner ->
                createEffect(fun () ->
                    // Re-render when any tracked signal inside View changes
                    this.InvokeAsync(Action(this.StateHasChanged)) |> ignore
                )
                scope <- Some owner
            )
        )

    override this.BuildRenderTree(builder) =
        this.View().Invoke(this, builder, 0) |> ignore

    interface IDisposable with
        member _.Dispose() = scope |> Option.iter _.Dispose()
```

---

## 5. Potential Challenges and Mitigations

1. **Package Maturity:**
   - *Challenge:* Ranvier is currently in pre-release preview.
   - *Mitigation:* Keep `RazorConsole.Ranvier` modular or as an optional package.
2. **Context Leakage:**
   - *Challenge:* Ranvier requires `graph.Run` or ambient graph activation before calling `create*`.
   - *Mitigation:* Encapsulate graph activation within component lifecycle hooks (`OnInitialized`).
3. **Sequence Alignment:**
   - *Challenge:* Dynamic lists in reactive graphs must maintain diff stability.
   - *Mitigation:* Use `builder.OpenRegion` within `View.forEach` to isolate sequence numbers.

---

## 6. Recommendations

1. Keep the current `RazorConsole.Elmish` implementation intact.
2. Extract `Node`, `Attr`, and future `View` DSL modules into a shared base layer.
3. Prototype a `RanvierComponent` in `.dsh-scratch/` to verify live terminal updates.
4. Add `RazorConsole.Ranvier` once the View DSL stabilizes.
