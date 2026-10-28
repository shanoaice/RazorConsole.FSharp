namespace RazorConsole.FSharp

open Microsoft.AspNetCore.Components.Rendering

/// <summary>
/// Emits attributes or parameters directly into the RenderTreeBuilder.
/// </summary>
type Attr = delegate of receiver: obj * builder: RenderTreeBuilder * sequence: int -> int

/// <summary>
/// Emits elements or component frames directly into the RenderTreeBuilder.
/// </summary>
type Node = delegate of receiver: obj * builder: RenderTreeBuilder * sequence: int -> int
