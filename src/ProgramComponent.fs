namespace RazorConsole.Elmish

open System.Threading.Tasks
open Elmish
open RazorConsole.FSharp

[<AbstractClass>]
type ProgramComponent<'model, 'msg>() =
    inherit MvuComponentBase<'model, 'msg>()

    let mutable _mappedProgram: Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node> option =
        None

    abstract Program: Program<ProgramComponent<'model, 'msg>, 'model, 'msg, Node>

    override this.OnInitialized() =
        let initModel, initCmd = Program.init this.Program <| this

        let view = Program.view this.Program <| initModel <| fun msg -> this.Dispatch msg

        let extractDispatch (dispatch: Dispatch<'msg>) =
            this.SetDispatch dispatch

        let initCmd = extractDispatch :: initCmd

        let mapInit _ = fun _ -> (initModel, initCmd)

        let mapSetState _ =
            fun model dispatch ->
                this.UpdateState(model, fun m -> Program.view this.Program <| m <| dispatch)

        this.SetInitialState(initModel, view)

        _mappedProgram <- Some(Program.map mapInit id id mapSetState id id this.Program)
        ()

    override this.OnAfterRenderAsync(firstRender) =
        if firstRender then
            let syncDispatch (dispatch) =
                fun msg ->
                    this.InvokeOnDispatcher(fun () ->
                        dispatch msg
                        Task.CompletedTask)
                    |> ignore

            _mappedProgram
            |> Option.iter (fun prog -> Program.runWithDispatch syncDispatch this prog)

            Task.CompletedTask
        else
            Task.CompletedTask
