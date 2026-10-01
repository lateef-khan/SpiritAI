using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>
/// GoTo Api Client.
/// </summary>
public sealed class GoToClient(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens,
    IOptions<GoToOptions> options)
{
    /// <summary>The GoTo Admin API host.</summary>
    public static readonly Uri AdminHost = new("https://api.getgo.com/");

    /// <summary>The page size GoTo allows and uses by default for channels.</summary>
    public const int PageSize = 100;

    /// <summary>The most users one Admin page holds.</summary>
    public const int AdminPageSize = 1000;

    /// <summary>The most calls one page of the call list holds.</summary>
    public const int ReportPageSize = 1000;

    private const string Channels = "notification-channel/v1/channels";

    private const string CallEventSubscriptions = "call-events/v1/subscriptions";

    private const string CallReportSubscriptions = "call-events-report/v1/subscriptions";

    private readonly GoToApi api = new(http, authorizer, tokens, options);

    /// <summary>
    /// Makes a webhook channel, or gives back the one that has this nickname and URL already.
    /// GoTo first sends <c>OPTIONS</c> to <paramref name="webhookUrl"/>, and refuses the channel
    /// unless the URL answers 2xx.
    /// </summary>
    /// <param name="nickname">Spirit's name for the channel.</param>
    /// <param name="webhookUrl">The public URL GoTo posts events to.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The channel GoTo made or found.</returns>
    public async Task<GoToChannel> CreateWebhookChannelAsync(
        string nickname, Uri webhookUrl, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentNullException.ThrowIfNull(webhookUrl);

        var body = new JsonObject
        {
            ["channelType"] = "Webhook",
            ["webhookChannelData"] = new JsonObject
            {
                ["webhook"] = new JsonObject { ["url"] = webhookUrl.AbsoluteUri },
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Channels}/{Uri.EscapeDataString(nickname)}")
        {
            Content = JsonContent.Create(body),
        };

        using var response = await api.SendAsync(request, cancellationToken).ConfigureAwait(false);

        return GoToChannel.Read(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists every channel of the PAT owner, all pages. GoTo cannot filter by nickname, so this is
    /// every environment's channels, and other apps' too.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The channels, newest first.</returns>
    public async Task<IReadOnlyList<GoToChannel>> ListChannelsAsync(CancellationToken cancellationToken = default)
    {
        var channels = new List<GoToChannel>();

        await foreach (var channel in api.ItemsAsync($"{Channels}?pageSize={PageSize}", cancellationToken).ConfigureAwait(false))
        {
            channels.Add(GoToChannel.Read(channel));
        }

        return channels;
    }

    /// <summary>Deletes a channel and its subscriptions. A channel that is gone already counts as deleted.</summary>
    /// <param name="nickname">The channel's <see cref="GoToChannel.Nickname"/>.</param>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task DeleteChannelAsync(string nickname, string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nickname);
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"{Channels}/{Uri.EscapeDataString(nickname)}/{Uri.EscapeDataString(channelId)}");

        try
        {
            using var response = await api.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Gone already: another server deleted it first, or GoTo's list was a few seconds behind.
        }
    }

    /// <summary>
    /// Subscribes a channel to the <c>STARTING</c>, <c>ACTIVE</c> and <c>ENDING</c> call events of the
    /// account in <see cref="GoToOptions.AccountKey"/>. A ringing line shows only in <c>ACTIVE</c>
    /// events. A repeat subscribe is harmless, but it does not change the events of a subscription
    /// that exists, and the read does not show them.
    /// </summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task SubscribeToCallEventsAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var body = new JsonObject
        {
            ["channelId"] = channelId,
            ["accountKeys"] = new JsonArray(new JsonObject
            {
                ["id"] = api.AccountKey(),
                ["events"] = new JsonArray("STARTING", "ACTIVE", "ENDING"),
            }),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, CallEventSubscriptions) { Content = JsonContent.Create(body) };
        using var response = await api.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A 207 carries one status per account key, and any of them can be a refusal.
        var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var answer = JsonDocument.Parse(said);

        if (answer.RootElement.GetProperty("accountKeys").EnumerateArray().Any(a => a.GetProperty("status").GetInt32() is < 200 or >= 300))
        {
            throw new HttpRequestException(
                $"GoTo refused the call-events subscription for an account key: {said}", inner: null, response.StatusCode);
        }
    }

    /// <summary>Reads which accounts' call events a channel is subscribed to.</summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account keys; empty when the channel has no subscription.</returns>
    public Task<IReadOnlyList<string>> ReadSubscribedAccountKeysAsync(string channelId, CancellationToken cancellationToken = default)
        => ReadAccountKeysAsync(CallEventSubscriptions, channelId, cancellationToken);

    /// <summary>
    /// Subscribes a channel to the account's <c>REPORT_SUMMARY</c> events: one post per call, a few
    /// minutes after it ends, once its report is ready. Needs
    /// <c>call-events.v1.notifications.manage</c>.
    /// </summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    public async Task SubscribeToCallReportsAsync(string channelId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        var accountKey = api.AccountKey();
        var body = new JsonObject
        {
            ["channelId"] = channelId,
            ["eventTypes"] = new JsonArray("REPORT_SUMMARY"),
            ["accountKeys"] = new JsonArray(accountKey),
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, CallReportSubscriptions) { Content = JsonContent.Create(body) };
        using var response = await api.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var said = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!SubscribesToSummaries(said, accountKey))
        {
            throw new HttpRequestException(
                $"GoTo did not subscribe the channel to the account's report summaries: {said}", inner: null, response.StatusCode);
        }
    }

    /// <summary>Reads which accounts' call reports a channel is subscribed to.</summary>
    /// <param name="channelId">The channel's <see cref="GoToChannel.ChannelId"/>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The account keys; empty when the channel has no report subscription.</returns>
    public Task<IReadOnlyList<string>> ReadReportSubscribedAccountKeysAsync(string channelId, CancellationToken cancellationToken = default)
        => ReadAccountKeysAsync(CallReportSubscriptions, channelId, cancellationToken);

    /// <summary>Every line in the account and the user who owns it. Needs <c>users.v1.lines.read</c>.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>One entry per line.</returns>
    public async Task<IReadOnlyList<GoToLineOwner>> ListLineOwnersAsync(CancellationToken cancellationToken = default)
    {
        var owners = new List<GoToLineOwner>();

        await foreach (var user in api
            .ItemsAsync($"users/v1/users?accountKey={Uri.EscapeDataString(api.AccountKey())}", cancellationToken)
            .ConfigureAwait(false))
        {
            var userKey = user.GetProperty("userKey").GetString()!;

            owners.AddRange(user.GetProperty("lines").EnumerateArray().Select(line => new GoToLineOwner(
                line.GetProperty("id").GetString()!,
                line.TryGetProperty("number", out var number) ? number.GetString() ?? string.Empty : string.Empty,
                userKey)));
        }

        return owners;
    }

    /// <summary>Every user in the account and their sign-in email. Needs <c>identity:</c>.</summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The email by user key.</returns>
    public async Task<IReadOnlyDictionary<string, string>> ListUserEmailsAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = Uri.EscapeDataString(api.AccountKey());
        var emails = new Dictionary<string, string>();

        for (var page = 0; page < GoToApi.MaxPages; page++)
        {
            var url = new Uri(
                AdminHost,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"admin/rest/v1/accounts/{accountKey}/users?attributes=key,email&offset={page * AdminPageSize}&pageSize={AdminPageSize}"));

            var results = (await api.GetAsync(url, cancellationToken).ConfigureAwait(false)).GetProperty("results");

            foreach (var user in results.EnumerateArray())
            {
                if (user.TryGetProperty("email", out var email) && email.GetString() is { Length: > 0 } address)
                {
                    emails[user.GetProperty("key").GetString()!] = address;
                }
            }

            if (results.GetArrayLength() < AdminPageSize)
            {
                return emails;
            }
        }

        throw new InvalidOperationException($"GoTo listed more than {GoToApi.MaxPages} pages of account users.");
    }

    /// <summary>
    /// One page of the calls that ended between <paramref name="start"/> and
    /// <paramref name="end"/>. GoTo allows at most 31 days. Needs <c>cr.v1.read</c>.
    /// </summary>
    /// <param name="start">The window's start.</param>
    /// <param name="end">The window's end.</param>
    /// <param name="pageMarker">The marker the last page gave, or null for the first.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page.</returns>
    public async Task<GoToReportSummaryPage> ListReportSummariesAsync(
        DateTimeOffset start, DateTimeOffset end, string? pageMarker, CancellationToken cancellationToken = default)
    {
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"call-events-report/v1/report-summaries?accountKey={Uri.EscapeDataString(api.AccountKey())}&startTime={Utc(start)}&endTime={Utc(end)}&pageSize={ReportPageSize}");

        if (pageMarker is not null)
        {
            path += $"&pageMarker={Uri.EscapeDataString(pageMarker)}";
        }

        return GoToReportSummaryPage.Read(await api.GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The full report of one call. Needs <c>cr.v1.read</c>.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The report, or null while GoTo does not have it: a call that has not ended.</returns>
    public async Task<GoToCallReport?> ReadReportAsync(string callId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);

        try
        {
            var report = await api.GetAsync(
                    new Uri($"call-events-report/v1/reports/{Uri.EscapeDataString(callId)}", UriKind.Relative), cancellationToken)
                .ConfigureAwait(false);

            return GoToCallReport.Read(report);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Every company number and the name of the dial plan it rings, such as <c>Spirit Start</c>.
    /// A number that rings a person's extension, a queue or a ring group is left out. Needs
    /// <c>voice-admin.v1.read</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the calls.</param>
    /// <returns>The dial plan's name by number in E.164; a number with no dial plan has no entry.</returns>
    public async Task<IReadOnlyDictionary<string, string>> ListCompanyLinesAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = Uri.EscapeDataString(api.AccountKey());
        var extensions = new Dictionary<string, string>();

        await foreach (var extension in api
            .ItemsAsync($"voice-admin/v1/extensions?accountKey={accountKey}&pageSize={PageSize}", cancellationToken)
            .ConfigureAwait(false))
        {
            if (Text(extension, "type") == "DIAL_PLAN" && Text(extension, "id") is { } id && Text(extension, "name") is { } name)
            {
                extensions[id] = name;
            }
        }

        var lines = new Dictionary<string, string>();

        await foreach (var number in api
            .ItemsAsync($"voice-admin/v1/phone-numbers?accountKey={accountKey}&pageSize={PageSize}", cancellationToken)
            .ConfigureAwait(false))
        {
            if (Text(number, "number") is { } line
                && Text(number, "routeTo", "id") is { } target
                && extensions.TryGetValue(target, out var plan))
            {
                lines[line] = plan;
            }
        }

        return lines;
    }

    private async Task<IReadOnlyList<string>> ReadAccountKeysAsync(string subscriptions, string channelId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);

        // Without channelId GoTo answers 400, which only means the id is missing.
        var answer = await api.GetAsync(
                new Uri($"{subscriptions}?channelId={Uri.EscapeDataString(channelId)}", UriKind.Relative), cancellationToken)
            .ConfigureAwait(false);

        return answer.TryGetProperty("accountKeys", out var keys)
            ? [.. keys.EnumerateArray().Select(k => k.GetString()!)]
            : [];
    }

    private static bool SubscribesToSummaries(string answer, string accountKey)
    {
        try
        {
            using var document = JsonDocument.Parse(answer);

            return Items(document.RootElement, "items").Any(item =>
                Text(item, "value") == accountKey
                && Items(item, "eventTypes").Any(type => type.ValueKind == JsonValueKind.String && type.GetString() == "REPORT_SUMMARY"));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Utc(DateTimeOffset at)
        => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
