# RazorConsole.FSharp

F# extensions and reactive UI paradigms for [RazorConsole](https://github.com/shanoaice/RazorConsole).

This repository provides sequence-threading view delegates and state-management adapters for terminal applications.
It renders terminal user interfaces using Microsoft Blazor and Spectre.Console.

## Packages

This project decouples view rendering from state management:

| Package | Role | Description |
| :--- | :--- | :--- |
| `RazorConsole.FSharp` | Core View Engine | Runtime-agnostic view delegates (`Node`, `Attr`), `MvuComponentBase`, and declarative `View` combinators. |
| `RazorConsole.Elmish` | State Adapter | Connects upstream `Elmish.Program` to Blazor lifecycles via `ProgramComponent<'model, 'msg>`. |
| `RazorConsole.Ranvier` *(Planned)* | State Adapter | Planned adapter connecting Ranvier reactive loops to Blazor's dispatcher. |

---

## Quick Start (Elmish)

Create an application component inheriting `ProgramComponent`:

```fsharp
namespace SampleApp

open Elmish
open RazorConsole.FSharp
open RazorConsole.Elmish

type Model = { Count: int }

type Msg =
    | Increment
    | Decrement

type CounterComponent() =
    inherit ProgramComponent<Model, Msg>()

    override _.Program =
        let init () = { Count = 0 }, Cmd.none

        let update msg model =
            match msg with
            | Increment -> { model with Count = model.Count + 1 }, Cmd.none
            | Decrement -> { model with Count = model.Count - 1 }, Cmd.none

        let view model dispatch =
            Node(fun receiver builder seq ->
                builder.OpenElement(seq, "div")
                builder.AddAttribute(seq + 1, "class", "panel")
                builder.AddAttribute(seq + 2, "data-header", "Counter")
                builder.AddContent(seq + 3, $"Count: {model.Count}")
                builder.CloseElement()
                seq + 4)

        Program.mkSimple init update view
```

Mount the component in `Program.fs` using `RazorConsole.Core`:

```fsharp
open Microsoft.Extensions.Hosting
open RazorConsole.Core

[<EntryPoint>]
let main args =
    Host.CreateDefaultBuilder(args)
        .UseRazorConsole<CounterComponent>()
        .Build()
        .Run()
    0
```

