using System.Net;
using System.Text;

namespace SpiritAI.Tests;

/// <summary>
/// Answers each request with the saved reply (or bare status) its route picks from
/// <c>{Folder}/Payloads</c>, whatever the count of retries.
/// </summary>
internal sealed class RoutingHandler(Func<HttpRequestMessage, (string? Payload, HttpStatusCode Status)> route) : HttpMessageHandler
{
    /// <summary>The test folder whose <c>Payloads</c> the replies are read from.</summary>
    public string Folder { get; init; } = "GoTo";

    public List<(string Method, string Url)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method, request.RequestUri!.ToString()));

        var (payload, status) = route(request);

        if (payload is null)
        {
            return new HttpResponseMessage(status);
        }

        var reply = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, Folder, "Payloads", payload + ".json"), cancellationToken);

        return new HttpResponseMessage(status) { Content = new StringContent(reply, Encoding.UTF8, "application/json") };
    }
}
