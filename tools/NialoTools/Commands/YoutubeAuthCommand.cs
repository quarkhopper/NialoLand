// YoutubeAuthCommand — performs the one-time OAuth 2.0 browser flow.
//
// Google's OAuth for desktop apps works like this:
//   1. We read the client_secret JSON (downloaded from Google Cloud Console)
//   2. We open the user's browser to a Google consent page
//   3. Google redirects back to localhost with an auth code
//   4. We exchange the code for an access token + refresh token
//   5. The Google client library stores the token in a local file store
//
// After this runs once, all other YouTube commands reuse the stored token
// silently — no browser needed again unless the token is revoked.
//
// Invoke: ntools youtube auth
//
// Requires: YOUTUBE_CLIENT_SECRET_PATH env var pointing to your client_secret.json

using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NialoTools.Commands;

public sealed class YoutubeAuthSettings : CommandSettings
{
    // --force re-runs the consent flow even if a token already exists.
    // Useful if you want to switch accounts or scopes.
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}

public sealed class YoutubeAuthCommand : AsyncCommand<YoutubeAuthSettings>
{
    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        YoutubeAuthSettings settings,
        CancellationToken cancellationToken)
    {
        var secretPath = Environment.GetEnvironmentVariable("YOUTUBE_CLIENT_SECRET_PATH");
        if (string.IsNullOrWhiteSpace(secretPath) || !File.Exists(secretPath))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] YOUTUBE_CLIENT_SECRET_PATH is not set or file not found.");
            AnsiConsole.MarkupLine("[grey]Set it to the path of your client_secret JSON from Google Cloud Console.[/]");
            return 1;
        }

        // Token store: persists the refresh token to %APPDATA%\NialoTools\youtube-token\
        // This directory is outside the repo — never committed.
        var tokenStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NialoTools", "youtube-token");

        if (settings.Force && Directory.Exists(tokenStorePath))
        {
            Directory.Delete(tokenStorePath, recursive: true);
            AnsiConsole.MarkupLine("[yellow]Cleared existing token — starting fresh.[/]");
        }

        AnsiConsole.MarkupLine("[bold]YouTube OAuth Authorization[/]");
        AnsiConsole.MarkupLine("[grey]Your browser will open to the Google consent screen...[/]\n");

        // Scopes we need:
        //   YouTubeScope.Readonly       — read channel/stream info
        //   YouTubeScope.ForceSsl       — required for live chat read
        //   "https://www.googleapis.com/auth/youtube" — post chat messages, manage stream
        var scopes = new[]
        {
            YouTubeService.Scope.YoutubeReadonly,
            YouTubeService.Scope.YoutubeForceSsl,
            YouTubeService.Scope.Youtube,
        };

        UserCredential credential;
        await using var secretStream = File.OpenRead(secretPath);

        try
        {
            // GoogleWebAuthorizationBroker opens the browser and handles the
            // local redirect server automatically — we don't need to set one up.
            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(secretStream).Secrets,
                scopes,
                user: "nialoland-stream",     // arbitrary user label for the token store
                cancellationToken,
                new FileDataStore(tokenStorePath, fullPath: true));
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("[red]Authorization cancelled.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Authorized![/] Token stored at: [grey]{tokenStorePath}[/]");
        AnsiConsole.MarkupLine($"[grey]Expiry: {credential.Token.IssuedUtc.AddSeconds(credential.Token.ExpiresInSeconds ?? 3600):u}[/]");
        AnsiConsole.MarkupLine("\n[grey]Run [/][cyan]ntools youtube status[/][grey] to verify.[/]");

        return 0;
    }
}
