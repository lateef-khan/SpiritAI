using System.Runtime.CompilerServices;

using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// A titler with no model behind it, standing in for <see cref="IConversationTitler"/>.
/// </summary>
internal sealed class SpellingTitler(IConversationStore conversations) : IConversationTitler
{
    public async IAsyncEnumerable<string> GenerateAsync(
        string conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var rows = await conversations.ReadAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (rows.Count == 0)
        {
            yield break;
        }

        var words = rows[0].Content.Text.Split(' ').Take(3).ToArray();

        for (var at = 0; at < words.Length; at++)
        {
            yield return at == 0 ? words[at] : " " + words[at];
        }

        await conversations.RenameAsync(conversationId, string.Join(' ', words), cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> GenerateFromAsync(
        string conversationId,
        IReadOnlyList<ChatMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var words = string.Join(' ', messages.Select(message => message.Text));

        if (string.IsNullOrWhiteSpace(words))
        {
            yield break;
        }

        var picked = words.Split(' ').Take(3).ToArray();

        for (var at = 0; at < picked.Length; at++)
        {
            yield return at == 0 ? picked[at] : " " + picked[at];
        }

        await conversations.RenameAsync(conversationId, string.Join(' ', picked), cancellationToken).ConfigureAwait(false);
    }
}
