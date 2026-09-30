using System.Security.Cryptography;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Desk users through Chatwoot's Platform API.
/// </summary>
public sealed class DeskUsers(HttpClient http, IOptions<ChatwootOptions> options)
{
    private readonly ChatwootApi api = new(http, options);

    private string Platform => $"{api.Settings.BaseUrl.TrimEnd('/')}/platform/api/v1";

    /// <summary>
    /// Whether the user is already a member of the Spirit account, as an agent or an administrator.
    /// </summary>
    public async Task<bool> IsInAccountAsync(int userId, CancellationToken cancellationToken)
    {
        var agents = await api.ReadAsync(HttpMethod.Get, $"{api.Account}/agents", body: null, cancellationToken, api.Settings.AdminToken).ConfigureAwait(false);

        return agents.EnumerateArray().Any(agent => agent.GetProperty("id").GetInt32() == userId);
    }

    /// <summary>
    /// Makes the user, or returns the Chatwoot user that already has this email. Chatwoot's
    /// Platform create adopts that user and leaves its password as it is.
    /// </summary>
    public async Task<int> CreateUserAsync(string name, string email, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["name"] = name,
            ["email"] = email,
            // Nobody keeps it: the Person only ever signs in through the one-time link.
            ["password"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) + "aA1!",
        };

        var created = await api.ReadAsync(HttpMethod.Post, $"{Platform}/users", body, cancellationToken, api.Settings.PlatformToken).ConfigureAwait(false);

        return created.GetProperty("id").GetInt32();
    }

    /// <summary>
    /// Chatwoot answers the same way whether this is the first time or not, but it also sets the
    /// role each time: call it on a member and an administrator becomes an agent.
    /// </summary>
    public async Task JoinAccountAsync(int userId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["user_id"] = userId, ["role"] = "agent" };

        await api.ReadAsync(HttpMethod.Post, $"{Platform}/accounts/{api.Settings.AccountId}/account_users", body, cancellationToken, api.Settings.PlatformToken)
            .ConfigureAwait(false);
    }

    /// <summary>Adds one member without disturbing the others; a PATCH here would replace the whole list.</summary>
    public async Task JoinInboxAsync(int userId, CancellationToken cancellationToken)
    {
        var body = new JsonObject { ["inbox_id"] = api.Settings.InboxId, ["user_ids"] = new JsonArray(userId) };

        await api.ReadAsync(HttpMethod.Post, $"{api.Account}/inbox_members", body, cancellationToken, api.Settings.AdminToken).ConfigureAwait(false);
    }

    public async Task<string> SignInLinkAsync(int userId, CancellationToken cancellationToken)
    {
        var login = await api.ReadAsync(HttpMethod.Get, $"{Platform}/users/{userId}/login", body: null, cancellationToken, api.Settings.PlatformToken)
            .ConfigureAwait(false);

        return login.GetProperty("url").GetString()!;
    }
}
