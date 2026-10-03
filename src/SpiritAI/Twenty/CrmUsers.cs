using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using SpiritAI.Hub;

namespace SpiritAI.Twenty;

/// <summary>CRM users through the Twenty fork's own endpoints.</summary>
public sealed class CrmUsers(HttpClient http, IOptions<TwentyOptions> twenty, IOptions<HubOptions> hub, TimeProvider clock)
{
    /// <summary>
    /// Makes the user, or returns the Twenty user that already has this email. The fork adopts
    /// that user and leaves its password and role as they are.
    /// </summary>
    /// <exception cref="CrmUnavailableException">
    /// Twenty could not be reached or timed out, answered with anything other than success or with
    /// a success that is not the fork's <c>{"id"}</c> (a sign-in page in front of it), or
    /// <see cref="TwentyOptions.BaseUrl"/> is empty, which the message says apart.
    /// </exception>
    public async Task<string> CreateUserAsync(string name, string email, CancellationToken cancellationToken)
    {
        var space = name.IndexOf(' ', StringComparison.Ordinal);
        
        var body = new JsonObject
        {
            ["email"] = email,
            ["firstName"] = space < 0 ? name : name[..space],
            ["lastName"] = space < 0 ? string.Empty : name[(space + 1)..],
        };

        var response = await SendAsync(HttpMethod.Post, "/auth/spirit/users", body, cancellationToken).ConfigureAwait(false);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new CrmUnavailableException();
            }

            try
            {
                var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
                return created.TryGetProperty("id", out var id) && id.GetString() is { Length: > 0 } value
                    ? value
                    : throw new CrmUnavailableException();
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
            {
                throw new CrmUnavailableException(ex);
            }
        }
    }

    /// <summary>Removes the member from the workspace. Their records keep existing with no owner.</summary>
    /// <exception cref="CrmRefusedException">The member is the workspace's last admin.</exception>
    /// <exception cref="CrmUnavailableException">Twenty could not be reached or answered with an error.</exception>
    public async Task DeleteUserAsync(string twentyUserId, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Delete, $"/auth/spirit/users/{Uri.EscapeDataString(twentyUserId)}", body: null, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is System.Net.HttpStatusCode.Conflict)
        {
            throw new CrmRefusedException();
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new CrmUnavailableException();
        }
    }

    public string SignInUrl(string twentyUserId)
        => $"{hub.Value.CrmUrl.TrimEnd('/')}/auth/spirit?note={Uri.EscapeDataString(HubNote.ForCrm(twentyUserId, twenty.Value.HubSecret, clock))}";

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, JsonObject? body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(twenty.Value.BaseUrl))
        {
            throw CrmUnavailableException.NotSetUp();
        }

        try
        {
            using var request = new HttpRequestMessage(method, $"{twenty.Value.BaseUrl.TrimEnd('/')}{path}");
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Authorization = new("Bearer", twenty.Value.HubSecret);

            return await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CrmUnavailableException(ex);
        }
    }
}
