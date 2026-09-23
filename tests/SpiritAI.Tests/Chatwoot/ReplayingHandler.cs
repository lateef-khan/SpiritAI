using System.Net;
using System.Net.Http.Headers;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// Stands in for Chatwoot at the wire. Keeps each request and answers every one with the same
/// saved Chatwoot reply from <c>Payloads</c>, or with an empty body when there is none.
/// </summary>
/// <param name="payload">The saved reply's file name, without <c>.json</c>.</param>
/// <param name="status">The status every answer carries.</param>
internal sealed class ReplayingHandler(string? payload, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public List<(string Url, string Body, string? Token)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var token = request.Headers.TryGetValues("api_access_token", out var tokens) ? tokens.Single() : null;

        Requests.Add((request.RequestUri!.ToString(), body, token));

        if (payload is null)
        {
            return new HttpResponseMessage(status);
        }

        var reply = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Chatwoot", "Payloads", payload + ".json"),
            cancellationToken);

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(reply, new MediaTypeHeaderValue("application/json")),
        };
    }
}
