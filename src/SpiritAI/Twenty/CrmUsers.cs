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
    /// <see cref="TwentyOptions.BaseUrl"/> is empty — which would otherwise fail as a relative URI.
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

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{twenty.Value.BaseUrl.TrimEnd('/')}/auth/spirit/users")
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Authorization = new("Bearer", twenty.Value.HubSecret);

            response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException
            || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new CrmUnavailableException(ex);
        }

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

    public string SignInUrl(string twentyUserId)
        => $"{hub.Value.CrmUrl.TrimEnd('/')}/auth/spirit?note={Uri.EscapeDataString(HubNote.ForCrm(twentyUserId, twenty.Value.HubSecret, clock))}";
}
