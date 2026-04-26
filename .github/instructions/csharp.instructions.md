---
description: "Use when writing C# code, .NET projects, console apps, or ASP.NET APIs. Covers .NET 8+ patterns, Spectre.Console, minimal APIs, and stream-friendly output."
applyTo: ["**/*.cs", "**/*.csproj", "**/*.sln"]
---

# C# / .NET Guidelines for NialoLand

## NialoTools Toolkit (tools/)

The `tools/` directory contains **NialoTools** — a .NET 9 CLI (`ntools`) that provides stream automation.  
It is **not** a stream session project; it's workspace infrastructure.

**Run during development:**
```powershell
dotnet run --project tools/NialoTools -- <command>
# e.g.
dotnet run --project tools/NialoTools -- stream status
dotnet run --project tools/NialoTools -- gh summary
```

**Adding a command:**
1. Create `tools/NialoTools/Commands/MyCommand.cs` — extend `Command<TSettings>` or `AsyncCommand<TSettings>`
2. Register in `tools/NialoTools/Program.cs` under the appropriate branch
3. `dotnet build tools/NialoTools` — must build clean before committing

**Spectre.Console.Cli v0.55+ note:** Override methods are `protected`, not `public`, and require a `CancellationToken` parameter:
```csharp
protected override int Execute(CommandContext context, MySettings settings, CancellationToken ct) { ... }
protected override async Task<int> ExecuteAsync(CommandContext context, MySettings settings, CancellationToken ct) { ... }
```

**Secrets in commands:** Read from env vars only — never CLI args (they appear in shell history):
```csharp
var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
    ?? throw new InvalidOperationException("GITHUB_TOKEN is not set");
```

## Project Setup

- Target `net8.0` or `net9.0` — use the latest LTS or current
- Use top-level statements for console apps — less boilerplate on camera
- `dotnet new` commands to scaffold: `console`, `web`, `webapi`, `blazor`

```bash
dotnet new console -n my-app -f net8.0
dotnet new webapi -n my-api -f net8.0 --no-openapi  # skip swagger clutter unless needed
```

## Terminal Output

Always use **Spectre.Console** for rich output — it makes streams look great:

```bash
dotnet add package Spectre.Console
```

Patterns to use:
- `AnsiConsole.MarkupLine("[green]Done![/]")` for colored status
- `AnsiConsole.Progress()` for progress bars
- `AnsiConsole.Live()` for real-time dashboards
- `AnsiConsole.Figlet()` for startup banners
- `AnsiConsole.Table()` for tabular data

## Minimal API Pattern (web services)

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/", () => "NialoLand API is live!");

app.Run();
```

- Add `/health` endpoint on every web service — Railway uses it
- Use `Results.Ok()`, `Results.NotFound()`, `Results.BadRequest()` — not manual status codes

## Configuration & Secrets

```csharp
// Good — read from env
var apiKey = Environment.GetEnvironmentVariable("MY_API_KEY")
    ?? throw new InvalidOperationException("MY_API_KEY is not set");

// Never — hardcode secrets
var apiKey = "sk-abc123...";
```

Use `appsettings.json` for non-secret config, env vars for secrets.

## Error Handling

Surface errors loudly — no silent catch blocks:

```csharp
// Good — visible, informative
catch (HttpRequestException ex)
{
    AnsiConsole.MarkupLine($"[red]HTTP error:[/] {ex.Message}");
    throw;
}

// Bad — swallowed
catch { }
```

## Naming & Style

- PascalCase: classes, methods, properties
- camelCase: local variables, parameters
- `async/await` throughout for I/O — never `.Result` or `.Wait()`
- Prefer `var` when the type is obvious from the right-hand side
- File-scoped namespaces: `namespace MyApp;`

## NuGet Packages to Know

| Package | Purpose |
|---------|---------|
| `Spectre.Console` | Rich terminal UI |
| `Spectre.Console.Cli` | CLI argument parsing |
| `Microsoft.Extensions.Http` | `HttpClient` factory |
| `System.Text.Json` | JSON (built-in, prefer over Newtonsoft) |
