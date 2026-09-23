using System.Threading.Channels;

using Microsoft.Extensions.Options;

namespace SpiritAI.Chatwoot;

/// <summary>
/// The chats waiting to be copied into Chatwoot. A chat is named, not a message: the copy reads
/// what the chat holds past its last copy, so a name dropped when the line is full is caught up
/// by the chat's next turn. Nothing is queued while the copy is not configured.
/// </summary>
public sealed class ChatwootCopyQueue(IOptions<ChatwootOptions> options)
{
    /// <summary>How many chats may wait. Past it the oldest waiting name is dropped.</summary>
    public const int Capacity = 1000;

    private readonly Channel<string> _chats = Channel.CreateBounded<string>(
        new BoundedChannelOptions(Capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>Asks for a chat to be copied.</summary>
    /// <param name="conversationId">The chat.</param>
    public void Enqueue(string conversationId)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        if (!options.Value.CopyEnabled)
        {
            return;
        }

        _chats.Writer.TryWrite(conversationId);
    }

    /// <summary>Reads the chats in the order they were asked for, until the host stops.</summary>
    /// <param name="cancellationToken">Stops the read.</param>
    /// <returns>The chats.</returns>
    public IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken)
        => _chats.Reader.ReadAllAsync(cancellationToken);
}
