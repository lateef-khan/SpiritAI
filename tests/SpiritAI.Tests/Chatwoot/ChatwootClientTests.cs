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

    [Theory]
    [InlineData(true, "on")]
    [InlineData(false, "off")]
    public async Task TypingGoesAsTheContactThroughThePublicApi(bool on, string status)
    {
        var wire = new ReplayingHandler(payload: null);
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            InboxIdentifier = "inbox-key",
        }));

        await client.ToggleTypingAsync("f33a4e94-b79d-4b64-986f-c3076d1c0109", 42, on, TestContext.Current.CancellationToken);

        var request = Assert.Single(wire.Requests);
        Assert.Equal(
            "http://chatwoot.test/public/api/v1/inboxes/inbox-key/contacts/f33a4e94-b79d-4b64-986f-c3076d1c0109/conversations/42/toggle_typing",
            request.Url);
        Assert.Equal($$"""{"typing_status":"{{status}}"}""", request.Body);
    }

    [Fact]
    public async Task AgentsAreListedAsTheServiceUserWithTheirStatus()
    {
        var wire = new ReplayingHandler("agents");
        var client = new ChatwootClient(new HttpClient(wire), Options.Create(new ChatwootOptions
        {
            BaseUrl = "http://chatwoot.test/",
            AccountId = 2,
            BotToken = "bot-token",
            ServiceToken = "service-token",
        }));

        var agents = await client.ListAgentsAsync(TestContext.Current.CancellationToken);

        Assert.Equal([new ChatwootAgent(2, "online"), new ChatwootAgent(3, "offline")], agents);

        var request = Assert.Single(wire.Requests);
        Assert.Equal("http://chatwoot.test/api/v1/accounts/2/agents", request.Url);
        Assert.Equal("service-token", request.Token);
    }
}
