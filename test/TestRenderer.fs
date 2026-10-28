namespace RazorConsole.Elmish.Test

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.RenderTree
open Microsoft.Extensions.Logging
open Microsoft.Extensions.Logging.Abstractions

/// A lightweight, in-memory Blazor Renderer designed for testing ComponentBase and ProgramComponent.
type TestRenderer(services: IServiceProvider, loggerFactory: ILoggerFactory) =
    inherit Renderer(services, loggerFactory)

    let dispatcher = Dispatcher.CreateDefault()

    new(services: IServiceProvider) =
        new TestRenderer(services, NullLoggerFactory.Instance)

    new() =
        let emptyServiceProvider =
            { new IServiceProvider with
                member _.GetService(_: Type) = null }
        new TestRenderer(emptyServiceProvider, NullLoggerFactory.Instance)

    override _.Dispatcher = dispatcher

    override _.UpdateDisplayAsync(_: inref<RenderBatch>) =
        Task.CompletedTask

    override _.HandleException(ex: exn) =
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex).Throw()

    member private this.AssignId(comp: IComponent) = this.AssignRootComponentId(comp)
    member private this.RenderRoot(id: int, p: ParameterView) = this.RenderRootComponentAsync(id, p)

    /// Inspect the latest render tree frames emitted by a component.
    member this.GetFrames(componentId: int) : ArrayRange<RenderTreeFrame> =
        this.GetCurrentRenderTreeFrames(componentId)

    /// Mount a root component onto the renderer and return its assigned componentId.
    member this.MountComponentAsync<'TComponent when 'TComponent :> IComponent>(comp: 'TComponent) : Task<int> =
        let compId = this.AssignId(comp)
        this.Dispatcher.InvokeAsync(fun () ->
            this.RenderRoot(compId, ParameterView.Empty)
        ).ContinueWith(fun (t: Task) ->
            if t.IsFaulted then
                match t.Exception with
                | null -> ()
                | agg ->
                    match agg.InnerException with
                    | null -> System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(agg).Throw()
                    | inner -> System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(inner).Throw()
            compId
        )

    /// Yield execution until all pending actions queued on the Blazor Dispatcher complete.
    member this.WaitForQuiescenceAsync() : Task =
        this.Dispatcher.InvokeAsync(fun () -> ())
