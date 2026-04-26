// GhSummaryCommand — prints a GitHub session summary.
//
// Shows: branch, commits ahead of main, open PRs, open issues.
// Saves a bunch of MCP round-trips when Copilot needs context about the
// current state of the repo — one command, one read.
//
// Invoke: ntools gh summary
//
// Requires: GITHUB_TOKEN env var with repo + read:user scope

using Spectre.Console;
using Spectre.Console.Cli;
using System.Net.Http.Headers;
using System.Text.Json;

namespace NialoTools.Commands;

public sealed class GhSummarySettings : CommandSettings
{
    // --owner / --repo let you target a different repo than the default.
    // Default: quarkhopper/NialoLand (the NialoLand public repo)
    [CommandOption("--owner <OWNER>")]
    public string Owner { get; init; } = "quarkhopper";

    [CommandOption("--repo <REPO>")]
    public string Repo { get; init; } = "NialoLand";
}

public sealed class GhSummaryCommand : AsyncCommand<GhSummarySettings>
{
    protected override async Task<int> ExecuteAsync(CommandContext context, GhSummarySettings settings, CancellationToken cancellationToken)
    {
        // Read token from env — never accept it as a CLI arg (it would appear in logs/history)
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] GITHUB_TOKEN environment variable is not set.");
            AnsiConsole.MarkupLine("[grey]Set it with: $env:GITHUB_TOKEN = 'your-token'[/]");
            return 1;
        }

        using var http = BuildClient(token);
        var repo = $"{settings.Owner}/{settings.Repo}";

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[grey]Fetching summary for {repo}...[/]", async ctx =>
            {
                // Fetch open PRs and issues in parallel to cut latency
                var prsTask    = GetJsonAsync(http, $"repos/{repo}/pulls?state=open&per_page=10");
                var issuesTask = GetJsonAsync(http, $"repos/{repo}/issues?state=open&per_page=10&labels=");

                await Task.WhenAll(prsTask, issuesTask);

                var prs    = prsTask.Result;
                var issues = issuesTask.Result;

                ctx.Status("[grey]Rendering...[/]");

                AnsiConsole.MarkupLine($"\n[bold]GitHub Summary[/] — [cyan]{repo}[/]\n");

                // Open PRs
                var prTable = new Table().Border(TableBorder.Rounded).AddColumn("PR").AddColumn("Title").AddColumn("Author");
                foreach (var pr in prs.EnumerateArray())
                {
                    prTable.AddRow(
                        $"[cyan]#{pr.GetProperty("number")}[/]",
                        Markup.Escape(pr.GetProperty("title").GetString() ?? ""),
                        Markup.Escape(pr.GetProperty("user").GetProperty("login").GetString() ?? "")
                    );
                }
                AnsiConsole.MarkupLine($"[bold]Open PRs:[/] {prs.GetArrayLength()}");
                if (prs.GetArrayLength() > 0) AnsiConsole.Write(prTable);

                // Open issues (filter out PRs — GitHub's issues endpoint includes them)
                var issueTable = new Table().Border(TableBorder.Rounded).AddColumn("Issue").AddColumn("Title");
                int issueCount = 0;
                foreach (var issue in issues.EnumerateArray())
                {
                    // Skip items that are actually PRs
                    if (issue.TryGetProperty("pull_request", out _)) continue;
                    issueTable.AddRow(
                        $"[yellow]#{issue.GetProperty("number")}[/]",
                        Markup.Escape(issue.GetProperty("title").GetString() ?? "")
                    );
                    issueCount++;
                }
                AnsiConsole.MarkupLine($"\n[bold]Open Issues:[/] {issueCount}");
                if (issueCount > 0) AnsiConsole.Write(issueTable);
            });

        return 0;
    }

    // Build an HttpClient with the GitHub API base address and auth header.
    // We set User-Agent — GitHub requires it; missing it returns 403.
    private static HttpClient BuildClient(string token)
    {
        var client = new HttpClient { BaseAddress = new Uri("https://api.github.com/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NialoTools/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient http, string path)
    {
        var response = await http.GetAsync(path);
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        // Clone so it outlives the using block
        return doc.RootElement.Clone();
    }
}
