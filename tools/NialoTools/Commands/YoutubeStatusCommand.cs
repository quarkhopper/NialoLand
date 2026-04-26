// YoutubeStatusCommand — shows your live stream status at a glance.
//
// Checks whether you're live, and if so shows:
//   - Stream title and viewer count
//   - Live chat ID (needed to post/read messages)
//   - Stream health (good / ok / bad / noData)
//   - Scheduled start time if not yet live
//
// Invoke: ntools youtube status
//
// Requires: run `ntools youtube auth` first to authorize

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NialoTools.Commands;

public sealed class YoutubeStatusSettings : CommandSettings { }

public sealed class YoutubeStatusCommand : AsyncCommand<YoutubeStatusSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        YoutubeStatusSettings settings,
        CancellationToken cancellationToken)
    {
        var youtube = await BuildServiceAsync(cancellationToken);
        if (youtube is null) return 1;

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("[grey]Checking stream status...[/]", async ctx =>
            {
                // Step 1: get the authenticated user's channel ID
                var channelRequest = youtube.Channels.List("snippet,status");
                channelRequest.Mine = true;
                var channelResponse = await channelRequest.ExecuteAsync(cancellationToken);
                var channel = channelResponse.Items?.FirstOrDefault();

                if (channel is null)
                {
                    AnsiConsole.MarkupLine("[red]No channel found for this account.[/]");
                    return;
                }

                AnsiConsole.MarkupLine($"\n[bold]Channel:[/] [cyan]{Markup.Escape(channel.Snippet.Title)}[/]");

                // Step 2: check for active live broadcasts
                var broadcastRequest = youtube.LiveBroadcasts.List("snippet,status,contentDetails");
                broadcastRequest.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Active;
                broadcastRequest.Mine = true;
                var broadcastResponse = await broadcastRequest.ExecuteAsync(cancellationToken);
                var broadcast = broadcastResponse.Items?.FirstOrDefault();

                if (broadcast is null)
                {
                    AnsiConsole.MarkupLine("[yellow]Not currently live.[/]");

                    // Check for upcoming streams
                    var upcomingRequest = youtube.LiveBroadcasts.List("snippet,status");
                    upcomingRequest.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Upcoming;
                    upcomingRequest.Mine = true;
                    var upcomingResponse = await upcomingRequest.ExecuteAsync(cancellationToken);
                    var upcoming = upcomingResponse.Items?.FirstOrDefault();

                    if (upcoming is not null)
                    {
                        var scheduled = upcoming.Snippet.ScheduledStartTimeDateTimeOffset;
                        AnsiConsole.MarkupLine($"[grey]Next stream:[/] [bold]{Markup.Escape(upcoming.Snippet.Title)}[/]");
                        if (scheduled.HasValue)
                            AnsiConsole.MarkupLine($"[grey]Scheduled:[/] {scheduled.Value.LocalDateTime:f}");
                    }
                    return;
                }

                // Live! Show details in a table
                var table = new Table().Border(TableBorder.Rounded).HideHeaders();
                table.AddColumn("Field");
                table.AddColumn("Value");

                table.AddRow("[grey]Title[/]",       Markup.Escape(broadcast.Snippet.Title ?? ""));
                table.AddRow("[grey]Status[/]",      $"[green]{broadcast.Status.LifeCycleStatus}[/]");
                table.AddRow("[grey]Privacy[/]",     broadcast.Status.PrivacyStatus ?? "");
                table.AddRow("[grey]Chat ID[/]",     $"[dim]{Markup.Escape(broadcast.Snippet.LiveChatId ?? "none")}[/]");

                // Stream health requires a LiveStream lookup via contentDetails
                var streamId = broadcast.ContentDetails?.BoundStreamId;
                if (!string.IsNullOrEmpty(streamId))
                {
                    var streamRequest = youtube.LiveStreams.List("status");
                    streamRequest.Id = streamId;
                    var streamResponse = await streamRequest.ExecuteAsync(cancellationToken);
                    var streamHealth = streamResponse.Items?.FirstOrDefault()?.Status?.HealthStatus?.Status ?? "noData";
                    var healthColor = streamHealth == "good" ? "green" : streamHealth == "ok" ? "yellow" : "red";
                    table.AddRow("[grey]Stream health[/]", $"[{healthColor}]{streamHealth}[/]");
                }

                AnsiConsole.MarkupLine("[bold green]LIVE[/]");
                AnsiConsole.Write(table);
            });

        return 0;
    }

    // Shared helper — loads the stored OAuth token and returns a YouTubeService.
    // Other YouTube commands can copy this pattern.
    internal static async Task<YouTubeService?> BuildServiceAsync(CancellationToken ct)
    {
        var secretPath = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET_PATH");
        if (string.IsNullOrWhiteSpace(secretPath) || !File.Exists(secretPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] YOUTUBE_CLIENT_SECRET_PATH is not set or file not found.");
            return null;
        }

        var tokenStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NialoTools", "youtube-token");

        if (!Directory.Exists(tokenStorePath))
        {
            AnsiConsole.MarkupLine("[red]Not authorized.[/] Run [cyan]ntools youtube auth[/] first.");
            return null;
        }

        await using var secretStream = File.OpenRead(secretPath);
        var scopes = new[]
        {
            YouTubeService.Scope.YoutubeReadonly,
            YouTubeService.Scope.YoutubeForceSsl,
            YouTubeService.Scope.Youtube,
        };

        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            GoogleClientSecrets.FromStream(secretStream).Secrets,
            scopes,
            user: "nialoland-stream",
            ct,
            new FileDataStore(tokenStorePath, fullPath: true));

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "NialoTools",
        });
    }
}
