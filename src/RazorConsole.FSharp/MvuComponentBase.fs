namespace RazorConsole.FSharp

open System
open System.Threading.Tasks
open Microsoft.AspNetCore.Components
open Microsoft.AspNetCore.Components.Rendering

/// <summary>
/// Abstract base class connecting MVU (Model-View-Update) and reactive loops
/// to the Microsoft Blazor component lifecycle and RenderTreeBuilder.
/// </summary>
[<AbstractClass>]
type MvuComponentBase<'model, 'msg>() =
    inherit ComponentBase()

    let mutable currentView: Node option = None
    let mutable currentModel: 'model option = None
    let mutable dispatchFunc: 'msg -> unit = fun _ -> ()

    /// <summary>
    /// Gets the current evaluated model, if initialized.
    /// </summary>
    member this.CurrentModel: 'model option = currentModel

    /// <summary>
    /// Gets the current evaluated view node, if initialized.
    /// </summary>
    member this.CurrentView: Node option = currentView

    /// <summary>
    /// Predicate controlling whether a model change triggers a view re-evaluation and re-render.
    /// Defaults to reference inequality: <c>not (obj.ReferenceEquals(oldModel, newModel))</c>.
    /// </summary>
    abstract ShouldRender: oldModel: 'model * newModel: 'model -> bool

    default this.ShouldRender(oldModel, newModel) =
        not (obj.ReferenceEquals(oldModel, newModel))

    /// <summary>
    /// Safe message dispatch entry point.
    /// </summary>
    member this.Dispatch: 'msg -> unit = fun msg -> dispatchFunc msg

    /// <summary>
    /// Sets or updates the internal dispatch delegate.
    /// </summary>
    member this.SetDispatch(dispatch: 'msg -> unit) =
        dispatchFunc <- dispatch

    /// <summary>
    /// Sets the initial model and view during component initialization.
    /// </summary>
    member this.SetInitialState(model: 'model, view: Node, ?dispatch: 'msg -> unit) =
        currentModel <- Some model
        currentView <- Some view
        match dispatch with
        | Some d -> dispatchFunc <- d
        | None -> ()

    member private this._StateHasChanged() = base.StateHasChanged()

    /// <summary>
    /// Requests a UI render pass marshaled to Blazor's Dispatcher.
    /// </summary>
    member this.RequestRender() =
        this.InvokeAsync(this._StateHasChanged) |> ignore

    /// <summary>
    /// Updates the state. If <see cref="ShouldRender"/> returns true, updates the cached view
    /// and requests a render.
    /// </summary>
    member this.UpdateState(newModel: 'model, renderView: 'model -> Node) =
        match currentModel with
        | Some oldModel when this.ShouldRender(oldModel, newModel) ->
            currentModel <- Some newModel
            currentView <- Some(renderView newModel)
            this.RequestRender()
        | _ ->
            currentModel <- Some newModel

    /// <summary>
    /// Marshals an asynchronous action onto Blazor's Dispatcher.
    /// </summary>
    member this.InvokeOnDispatcher(action: unit -> Task) : Task =
        this.InvokeAsync(action)

    /// <summary>
    /// Marshals a synchronous action onto Blazor's Dispatcher.
    /// </summary>
    member this.InvokeOnDispatcher(action: unit -> unit) : Task =
        this.InvokeAsync(action)

    /// <summary>
    /// Renders the current view node into the Blazor RenderTreeBuilder.
    /// </summary>
    override this.BuildRenderTree(builder: RenderTreeBuilder) =
        currentView
        |> Option.iter (fun (view: Node) -> view.Invoke(this, builder, 0) |> ignore)
