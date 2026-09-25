using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Options;

namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo Users and Admin calls. The Admin API is on its own host, <see cref="AdminHost"/>.
/// </summary>
public sealed class GoToDirectoryApiClient(
    HttpClient http,
    IGoToRequestAuthorizer authorizer,
    IGoToAuthTokenProvider tokens,
    IOptions<GoToOptions> options)
    : IGoToDirectoryApiClient
{
    /// <summary>The GoTo Admin API host.</summary>
    public static readonly Uri AdminHost = new("https://api.getgo.com/");

    /// <summary>The most users one Admin page holds.</summary>
    public const int AdminPageSize = 1000;

    private const int MaxPages = 20;

    /// <inheritdoc />
    public async Task<IReadOnlyList<GoToLineOwner>> ListLineOwnersAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = Uri.EscapeDataString(AccountKey());
        var owners = new List<GoToLineOwner>();
        string? marker = null;

        for (var page = 0; page < MaxPages; page++)
        {
            var path = marker is null
                ? $"users/v1/users?accountKey={accountKey}"
                : $"users/v1/users?accountKey={accountKey}&pageMarker={Uri.EscapeDataString(marker)}";

            var answer = await GetAsync(new Uri(path, UriKind.Relative), cancellationToken).ConfigureAwait(false);

            foreach (var user in answer.GetProperty("items").EnumerateArray())
            {
                var userKey = user.GetProperty("userKey").GetString()!;

                owners.AddRange(user.GetProperty("lines").EnumerateArray().Select(line => new GoToLineOwner(
                    line.GetProperty("id").GetString()!,
                    line.TryGetProperty("number", out var number) ? number.GetString() ?? string.Empty : string.Empty,
                    userKey)));
            }

            marker = answer.TryGetProperty("nextPageMarker", out var next) ? next.GetString() : null;

            if (string.IsNullOrEmpty(marker))
            {
                return owners;
            }
        }

        throw new InvalidOperationException($"GoTo listed more than {MaxPages} pages of users.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> ListUserEmailsAsync(CancellationToken cancellationToken = default)
    {
        var accountKey = Uri.EscapeDataString(AccountKey());
        var emails = new Dictionary<string, string>();

        for (var page = 0; page < MaxPages; page++)
        {
            var url = new Uri(
                AdminHost,
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"admin/rest/v1/accounts/{accountKey}/users?attributes=key,email&offset={page * AdminPageSize}&pageSize={AdminPageSize}"));

            var answer = await GetAsync(url, cancellationToken).ConfigureAwait(false);
            var results = answer.GetProperty("results");

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

        throw new InvalidOperationException($"GoTo listed more than {MaxPages} pages of account users.");
    }

    private string AccountKey()
    {
        var accountKey = options.Value.AccountKey;

        return string.IsNullOrWhiteSpace(accountKey)
            ? throw new InvalidOperationException($"{GoToOptions.SectionName}:{nameof(GoToOptions.AccountKey)} is missing.")
            : accountKey;
    }

    private async Task<JsonElement> GetAsync(Uri url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await GoToApiCall.SendAsync(http, authorizer, tokens, request, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
    }
}
