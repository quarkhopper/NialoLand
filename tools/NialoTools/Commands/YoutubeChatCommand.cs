// YoutubeChatCommand — tails live chat to the terminal in real-time.
//
// The YouTube Live Chat API works by polling — there's no WebSocket.
// Google tells us the minimum polling interval (pollingIntervalMillis)
// and we must respect it or we'll get rate-limited. We always wait at
// least that long between requests.
//
// Output is color-coded by message type:
//   White       — regular chat
//   Gold        — Super Chat (paid message)
//   Blue        — new member / membership milestone
//   Red         — message removed / user banned
//
// Invoke: ntools youtube chat
//    or:  ntools youtube chat --broadcast <broadcastId>
//
// Requires: run `ntools youtube auth` first

using Google.Apis.YouTube.v3;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NialoTools.Commands;

public sealed class YoutubeChatSettings : CommandSettings
{
    // --broadcast lets you target a specific broadcast ID.
    // Defaults to the currently active live broadcast.
    [CommandOption("-b|--broadcast <ID>")]
    public string? BroadcastId { get; init; }

    // --max-results controls how many messages are fetched per poll (1-2000).
    [CommandOption("-n|--max-results <N>")]
    public int MaxResults { get; init; } = 200;
}

public sealed class YoutubeChatCommand : AsyncCommand<YoutubeChatSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        YoutubeChatSettings settings,
        CancellationToken cancellationToken)
    {
        var youtube = await YoutubeStatusCommand.BuildServiceAsync(cancellationToken);
        if (youtube is null) return 1;

        // Find the live chat ID — either from the specified broadcast or the active one
        string? liveChatId = null;

        if (!string.IsNullOrEmpty(settings.BroadcastId))
        {
            var req = youtube.LiveBroadcasts.List("snippet");
            req.Id = settings.BroadcastId;
            var resp = await req.ExecuteAsync(cancellationToken);
            liveChatId = resp.Items?.FirstOrDefault()?.Snippet?.LiveChatId;
        }
        else
        {
            // Find the currently active broadcast
            var req = youtube.LiveBroadcasts.List("snippet,status");
            req.BroadcastStatus = LiveBroadcastsResource.ListRequest.BroadcastStatusEnum.Active;
            req.Mine = true;
            var resp = await req.ExecuteAsync(cancellationToken);
            liveChatId = resp.Items?.FirstOrDefault()?.Snippet?.LiveChatId;
        }

        if (string.IsNullOrEmpty(liveChatId))
        {
            AnsiConsole.MarkupLine("[red]No active live chat found.[/] Are you live?");
            AnsiConsole.MarkupLine("[grey]Use --broadcast <id> to target a specific broadcast.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Connected to live chat.[/] [grey]Ctrl+C to stop.[/]\n");

        string? pageToken = null;

        // Poll loop — runs until Ctrl+C triggers the CancellationToken
        while (!cancellationToken.IsCancellationRequested)
        {
            var chatRequest = youtube.LiveChatMessages.List(liveChatId, "snippet,authorDetails");
            chatRequest.MaxResults = settings.MaxResults;
            if (pageToken is not null) chatRequest.PageToken = pageToken;

            Google.Apis.YouTube.v3.Data.LiveChatMessageListResponse? chatResponse = null;
            try
            {
                chatResponse = await chatRequest.ExecuteAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Poll error:[/] {Markup.Escape(ex.Message)}");
                // Back off a bit before retrying rather than hammering the API on errors
                await Task.Delay(5000, cancellationToken).ConfigureAwait(false);
                continue;
            }

            foreach (var message in chatResponse.Items ?? [])
            {
                RenderMessage(message);
            }

            pageToken = chatResponse.NextPageToken;

            // Respect the polling interval Google gives us — going faster gets you rate-limited
            var delay = (int)(chatResponse.PollingIntervalMillis ?? 5000);
            try { await Task.Delay(delay, cancellationToken); }
            catch (OperationCanceledException) { break; }
        }

        AnsiConsole.MarkupLine("\n[grey]Chat stream ended.[/]");
        return 0;
    }

    private static void RenderMessage(Google.Apis.YouTube.v3.Data.LiveChatMessage message)
    {
        var author  = Markup.Escape(message.AuthorDetails?.DisplayName ?? "unknown");
        var snippet = message.Snippet;
        var type    = snippet?.Type ?? "textMessageEvent";

        // Timestamp in local time — easier to correlate with stream events
        var time = snippet?.PublishedAtDateTimeOffset?.LocalDateTime.ToString("HH:mm:ss") ?? "--:--:--";

        switch (type)
        {
            case "superChatEvent":
                // Super Chats are paid — always worth showing prominently
                var amount = snippet?.SuperChatDetails?.AmountDisplayString ?? "?";
                var superText = Markup.Escape(snippet?.SuperChatDetails?.UserComment ?? "");
                AnsiConsole.MarkupLine($"[grey]{time}[/] [gold3]💰 {author} ({amount}):[/] {superText}");
                break;

            case "memberMilestoneChatEvent":
            case "newSponsorEvent":
                AnsiConsole.MarkupLine($"[grey]{time}[/] [blue]⭐ {author} became a member![/]");
                break;

            case "messageDeletedEvent":
                AnsiConsole.MarkupLine($"[grey]{time}[/] [red dim](message deleted)[/]");
                break;

            case "userBannedEvent":
                AnsiConsole.MarkupLine($"[grey]{time}[/] [red]🚫 {author} was banned.[/]");
                break;

            default:
                // Regular chat message
                var text = Markup.Escape(snippet?.DisplayMessage ?? snippet?.TextMessageDetails?.MessageText ?? "");
                AnsiConsole.MarkupLine($"[grey]{time}[/] [cyan]{author}:[/] {text}");
                break;
        }
    }
}
