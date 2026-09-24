using System.Net;
using System.Net.Http.Headers;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// Stands in for Chatwoot at the wire. Keeps each request and answers it with a saved Chatwoot
/// reply from <c>Payloads</c>, or with an empty body when there is none.
/// </summary>
/// <param name="replies">
/// Each saved reply's file name, without <c>.json</c>, and its status, one per request in order.
/// The last one answers every request after it.
/// </param>
internal sealed class ReplayingHandler(IReadOnlyList<(string? Payload, HttpStatusCode Status)> replies) : HttpMessageHandler
{
    /// <summary>Answers each request with the next saved reply, all with one status.</summary>
    /// <param name="payloads">The saved replies' file names, without <c>.json</c>.</param>
    /// <param name="status">The status every answer carries.</param>
    public ReplayingHandler(IReadOnlyList<string?> payloads, HttpStatusCode status = HttpStatusCode.OK)
        : this([.. payloads.Select(p => (p, status))])
    {
    }

    /// <summary>Answers every request with the same saved reply.</summary>
    /// <param name="payload">The saved reply's file name, without <c>.json</c>.</param>
    /// <param name="status">The status every answer carries.</param>
    public ReplayingHandler(string? payload, HttpStatusCode status = HttpStatusCode.OK)
        : this([(payload, status)])
    {
    }

    public List<(string Url, string Body, string? Token)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        var token = request.Headers.TryGetValues("api_access_token", out var tokens) ? tokens.Single() : null;

        var (payload, status) = replies[Math.Min(Requests.Count, replies.Count - 1)];

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
