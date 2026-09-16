using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Microsoft.Extensions.Options;

using Resend;

using SpiritAI.Handoffs.Mail;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Mail;

/// <summary>
/// The adapter over Resend, section 8 of the handoff spec: plain text, and the staff mailbox as
/// the reply address. Read at the wire, as the JSON Resend's <c>POST /emails</c> takes, because
/// the SDK's client is the one seam it offers that does not need a hundred-member fake.
/// </summary>
public sealed class ResendHandoffMailerTests
{
    [Fact]
    public async Task AReplyGoesOutAsPlainTextFromSupportWithTheStaffMailboxToAnswerTo()
    {
        var wire = new RecordingHandler();
        var resend = ResendClient.Create(new ResendClientOptions { ApiToken = "re_test" }, new HttpClient(wire));
        var mailer = new ResendHandoffMailer(
            resend,
            Options.Create(new HandoffMailOptions
            {
                From = "Spirit Support <support@spiritfitness.com>",
                ReplyTo = "support@spiritfitness.com",
            }));

        await mailer.SendReplyAsync(
            new HandoffReplyMail("pat@example.com", "call-1", "Dana R.", "Try the tension bolt."),
            TestContext.Current.CancellationToken);

        using var sent = JsonDocument.Parse(Assert.Single(wire.Bodies));
        var email = sent.RootElement;
        Assert.Equal("Spirit Support <support@spiritfitness.com>", email.GetProperty("from").GetString());
        Assert.Equal(["pat@example.com"], email.GetProperty("to").EnumerateArray().Select(to => to.GetString()));
        Assert.Equal(
            ["support@spiritfitness.com"],
            email.GetProperty("reply_to").EnumerateArray().Select(to => to.GetString()));
        Assert.Equal("Reply from Spirit support", email.GetProperty("subject").GetString());
        Assert.Equal(
            "Dana R. from Spirit replied to your chat:\n\nTry the tension bolt.\n\nReply to this email to continue.",
            email.GetProperty("text").GetString());
        Assert.False(email.TryGetProperty("html", out _));
    }

    /// <summary>Stands in for Resend: keeps every request body and answers as a send accepted.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"id":"{{Guid.NewGuid()}}"}""",
                    new MediaTypeHeaderValue("application/json")),
            };
        }
    }
}
