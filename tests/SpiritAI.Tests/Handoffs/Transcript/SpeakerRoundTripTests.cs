using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.Tests.Database;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Transcript;

/// <summary>
/// The one fact about the speaker only PostgreSQL can keep: a staff reply appended outside any
/// turn comes back out of AgentCore's own store with its speaker still on it, in the shape the
/// browser reads. Section 4.3 of the handoff spec.
/// </summary>
public sealed class SpeakerRoundTripTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AStaffReplyComesBackSigned()
    {
        var conversationId = "test-" + Guid.NewGuid().ToString("N");
        var store = await fixture.OpenConversationStoreAsync();

        try
        {
            await store.CreateAsync(conversationId, TestContext.Current.CancellationToken);

            var reply = new ChatMessage(ChatRole.Assistant, "Try the tension bolt.");
            SpeakerProperty.Attach(reply, HandoffSpeaker.Human("Dana R.", "Support"));

            var written = await store.AppendMessageAsync(conversationId, reply, TestContext.Current.CancellationToken);

            var row = Assert.Single(await store.ReadAsync(conversationId, TestContext.Current.CancellationToken));
            Assert.Equal(written.MessageId, row.MessageId);
            Assert.Equal(ChatRole.Assistant, row.Content.Role);
            Assert.Equal("Try the tension bolt.", row.Content.Text);

            var speaker = SpeakerProperty.Read(row.Content);
            Assert.NotNull(speaker);
            Assert.Equal("human", speaker.Value.GetProperty("kind").GetString());
            Assert.Equal("Dana R.", speaker.Value.GetProperty("name").GetString());
            Assert.Equal("Support", speaker.Value.GetProperty("detail").GetString());
        }
        finally
        {
            await fixture.DeleteConversationAsync(conversationId);

            if (store is IAsyncDisposable pool)
            {
                await pool.DisposeAsync();
            }
        }
    }
}
