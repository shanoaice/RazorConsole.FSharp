# Testing

Run unit tests with the .NET CLI:

```bash
dotnet test
```

Unit tests use an in-memory `TestRenderer`.
The harness inspects Blazor's linear render tree diff stream without launching a terminal window.
