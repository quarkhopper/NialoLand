// NialoTools — NialoLand stream automation toolkit
//
// This CLI is the Swiss-army knife for the NialoLand vibe-coding stream.
// Commands live in Commands/ — each command is a self-contained class.
//
// Usage: ntools <command> [options]
// Run:   dotnet run --project tools/NialoTools -- <command>
// Build: dotnet build tools/NialoTools

using NialoTools.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

// Spectre.Console.Cli gives us a clean, self-documenting CLI with
// automatic --help generation and typed settings — no manual arg parsing.
var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("ntools");
    config.SetApplicationVersion("0.1.0");

    // ── Stream Commands ───────────────────────────────────────────────────────
    // Commands that support the live stream workflow.
    config.AddBranch("stream", stream =>
    {
        stream.SetDescription("Stream session management");
        stream.AddCommand<StreamStatusCommand>("status")
              .WithDescription("Show current session status (branch, recent commits, open issues)");
    });

    // ── GitHub Commands ───────────────────────────────────────────────────────
    // Automation that would otherwise require many Copilot round-trips.
    config.AddBranch("gh", gh =>
    {
        gh.SetDescription("GitHub automation");
        gh.AddCommand<GhSummaryCommand>("summary")
          .WithDescription("Print a session summary (branch, commits since main, open PRs)");
    });

    // ── YouTube Commands ──────────────────────────────────────────────────────
    // Live stream automation — auth once, then use status/chat freely.
    config.AddBranch("youtube", yt =>
    {
        yt.SetDescription("YouTube stream automation");
        yt.AddCommand<YoutubeAuthCommand>("auth")
          .WithDescription("One-time OAuth authorization (opens browser)");
        yt.AddCommand<YoutubeStatusCommand>("status")
          .WithDescription("Show live stream status, viewer count, and stream health");
        yt.AddCommand<YoutubeChatCommand>("chat")
          .WithDescription("Tail live chat to the terminal in real-time");
    });

    // More command groups to add as the stream needs grow:
    //   railway — deploy status, logs tail
    //   report  — generate session recap
    //   env     — validate required env vars are set
});

try
{
    return app.Run(args);
}
catch (Exception ex)
{
    // Surface errors loudly — a visible crash beats a silent one on stream.
    AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    return 1;
}
