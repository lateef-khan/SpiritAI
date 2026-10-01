using System.Net;
using System.Text;

namespace SpiritAI.Tests.GoTo;

/// <summary>Answers its requests in order with the JSON bodies it is given; the last body answers every request after it.</summary>
internal sealed class AnsweringHandler(params string[] bodies) : HttpMessageHandler
{
    private int next;

    public List<(string Method, string Url)> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add((request.Method.Method, request.RequestUri!.ToString()));

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(bodies[Math.Min(next++, bodies.Length - 1)], Encoding.UTF8, "application/json"),
        });
    }
}
