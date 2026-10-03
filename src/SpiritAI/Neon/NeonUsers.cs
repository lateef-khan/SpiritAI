using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SpiritAI.Database;

namespace SpiritAI.Neon;

/// <summary>Neon Auth users, made and deleted with the project's API key.</summary>
public sealed class NeonUsers(HttpClient http, IOptions<NeonOptions> options, SpiritDbContext db)
{
    /// <summary>Makes a sign-in. The person signs in with the email code; nobody gets a password.</summary>
    /// <exception cref="NeonEmailTakenException">Neon already has this email.</exception>
    /// <exception cref="NeonUnavailableException">Neon failed, or is not set up.</exception>
    public async Task<Guid> CreateAsync(string email, string name, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Post, "users", JsonContent.Create(new { email, name }), cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.BadRequest
            && (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))
                .Contains("User already exists", StringComparison.OrdinalIgnoreCase))
        {
            throw new NeonEmailTakenException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new NeonUnavailableException();
        }

        Guid id;
        try
        {
            var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            id = created.GetProperty("id").GetGuid();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or FormatException or InvalidOperationException)
        {
            throw new NeonUnavailableException(ex);
        }

        if (options.Value.CopyToLocal)
        {
            await db.Database.ExecuteSqlAsync(
                $"""INSERT INTO neon_auth."user" (id, name, email, "emailVerified") VALUES ({id}, {name}, {email}, true) ON CONFLICT (id) DO NOTHING""",
                cancellationToken).ConfigureAwait(false);
        }

        return id;
    }

    /// <summary>Deletes a sign-in. One Neon does not have counts as deleted.</summary>
    /// <exception cref="NeonUnavailableException">Neon failed, or is not set up.</exception>
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"users/{userId}", content: null, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
        {
            throw new NeonUnavailableException();
        }

        if (options.Value.CopyToLocal)
        {
            await db.Database.ExecuteSqlAsync($"""DELETE FROM neon_auth."user" WHERE id = {userId}""", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        if (!settings.IsSetUp)
        {
            throw NeonUnavailableException.NotSetUp();
        }

        var url = $"{settings.ApiUrl.TrimEnd('/')}/projects/{Uri.EscapeDataString(settings.ProjectId)}/branches/{Uri.EscapeDataString(settings.BranchId)}/auth/{path}";
        using var request = new HttpRequestMessage(method, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);

        try
        {
            return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new NeonUnavailableException(ex);
        }
    }
}
