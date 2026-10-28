namespace RazorConsole.Elmish

open System.Threading.Tasks
open Microsoft.AspNetCore.Components
open Elmish

[<AbstractClass>]
type ProgramComponent<'model, 'msg>() =
    inherit ComponentBase()

    let mutable _currentView: Node option = None
    let mutable _currentModel: 'model option = None
    let mutable _elmishDispatch: Dispatch<'msg> = fun _ -> ()

    let mutable _mappedProgram: Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node> option =
        None

    abstract Program: Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node>

    abstract ShouldRender: oldModel: 'model * newModel: 'model -> bool

    default this.ShouldRender(oldModel, newModel) =
        not (obj.ReferenceEquals(oldModel, newModel))

    member this.Dispatch: 'msg -> unit = fun msg -> _elmishDispatch msg

    member private this._StateHasChanged() = base.StateHasChanged()

    member private this.RequestRender() =
        this.InvokeAsync this._StateHasChanged |> ignore

    member private this._InvokeAsync(action) = this.InvokeAsync action

    override this.OnInitialized() =
        let initModel, initCmd = Program.init this.Program <| this

        let view = Program.view this.Program <| initModel <| fun msg -> this.Dispatch msg

        let extractDispatch (dispatch: Dispatch<'msg>) =
            _elmishDispatch <- dispatch
            ()

        let initCmd = extractDispatch :: initCmd

        let mapInit _ = fun _ -> (initModel, initCmd)

        let mapSetState _ =
            fun model dispatch ->
                if this.ShouldRender(_currentModel.Value, model) then
                    _currentView <- Some(Program.view this.Program <| model <| dispatch)
                    _currentModel <- Some model
                    this.RequestRender()
                else
                    _currentModel <- Some model

        _currentView <- Some view
        _currentModel <- Some initModel

        _mappedProgram <- Some(Program.map mapInit id id mapSetState id id this.Program)
        ()

    override this.BuildRenderTree(builder) =
        _currentView
        |> Option.iter (fun (view: Node) -> view.Invoke(this, builder, 0) |> ignore)

    override this.OnAfterRenderAsync(firstRender) =
        if firstRender then
            let syncDispatch (dispatch) =
                fun msg ->
                    this._InvokeAsync (fun () ->
                        dispatch msg
                        Task.CompletedTask)
                    |> ignore

            _mappedProgram
            |> Option.iter (fun prog -> Program.runWithDispatch syncDispatch this prog)

            Task.CompletedTask
        else
            Task.CompletedTask
