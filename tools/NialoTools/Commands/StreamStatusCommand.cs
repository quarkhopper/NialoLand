// StreamStatusCommand — shows the current stream session at a glance.
//
// Displays: active git branch, recent commits, open GitHub issues.
// This replaces several manual git commands that would otherwise eat
// tokens or screen time at the start of a stream.
//
// Invoke: ntools stream status

using Spectre.Console;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace NialoTools.Commands;

// Settings hold the parsed CLI arguments for this command.
// Add [CommandOption] properties here as the command grows.
public sealed class StreamStatusSettings : CommandSettings
{
    // --repo lets you override the assumed repo path (defaults to cwd)
    [CommandOption("-r|--repo <PATH>")]
    public string? RepoPath { get; init; }
}

public sealed class StreamStatusCommand : Command<StreamStatusSettings>
{
    protected override int Execute(CommandContext context, StreamStatusSettings settings, CancellationToken cancellationToken)
    {
        var repoRoot = settings.RepoPath ?? Directory.GetCurrentDirectory();

        AnsiConsole.Write(new FigletText("NialoLand").Color(Color.HotPink));
        AnsiConsole.MarkupLine("[grey]Stream Status[/]\n");

        // Git branch
        var branch = RunGit("rev-parse --abbrev-ref HEAD", repoRoot);
        AnsiConsole.MarkupLine($"[bold]Branch:[/] [cyan]{branch}[/]");

        // Last 5 commits — gives a quick "where were we?" on stream
        var log = RunGit("log --oneline -5", repoRoot);
        if (!string.IsNullOrWhiteSpace(log))
        {
            AnsiConsole.MarkupLine("\n[bold]Recent commits:[/]");
            foreach (var line in log.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                // Escape brackets so Spectre doesn't interpret them as markup
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(line)}[/]");
            }
        }

        // Uncommitted changes
        var status = RunGit("status --short", repoRoot);
        if (string.IsNullOrWhiteSpace(status))
        {
            AnsiConsole.MarkupLine("\n[green]Working tree clean[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"\n[yellow]Uncommitted changes:[/]");
            foreach (var line in status.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                AnsiConsole.MarkupLine($"  [grey]{Markup.Escape(line)}[/]");
            }
        }

        return 0;
    }

    // Helper: run a git command and return stdout. Returns empty string on failure.
    // We don't throw here — stream status is informational; partial output is fine.
    private static string RunGit(string arguments, string workingDirectory)
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return output;
        }
        catch
        {
            return string.Empty;
        }
    }
}
