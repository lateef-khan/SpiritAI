using System.Net;
using System.Net.Http.Headers;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;

using Xunit;

namespace SpiritAI.Tests.Chatwoot;

/// <summary>
/// The Chatwoot calls at the wire. The replies are the ones a live Chatwoot sent, kept in
/// <c>Payloads</c>.
/// </summary>
public sealed class ChatwootClientTests
{
    [Fact]
    public async Task AContactIsMadeByNameAndChatwootsOwnIdsComeBack()
    {
        var wire = new ReplayingHandler("contact_created");
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            InboxIdentifier = "inbox-key",
        }));

        var made = await client.CreateContactAsync("Visitor 7", TestContext.Current.CancellationToken);

        Assert.Equal(new ChatwootContact(10, "f33a4e94-b79d-4b64-986f-c3076d1c0109"), made);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts", request.Url);
        Assert.Equal("""{"name":"Visitor 7"}""", request.Body);
    }

    /// <summary>Keeps each request and answers every one with the same saved Chatwoot reply.</summary>
    private sealed class ReplayingHandler(string payload) : HttpMessageHandler
    {
        public List<(string Url, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(cancellationToken)));

            var reply = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "Chatwoot", "Payloads", payload + ".json"),
                cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(reply, new MediaTypeHeaderValue("application/json")),
            };
        }
    }
}
