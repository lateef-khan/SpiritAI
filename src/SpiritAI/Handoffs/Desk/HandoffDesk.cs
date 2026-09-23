using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.Store;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// The visitor's side of a handoff: the ask, where the chat stands, and the words the visitor says
/// while a person is on the way or on the chat.
/// </summary>
public sealed class HandoffDesk(
    IHandoffStore handoffs,
    IConversations conversations,
    IHandoffNotifier notifier,
    IStaffPresence staff,
    TimeProvider clock)
{
    /// <summary>The role a visitor's message is stored and pushed under.</summary>
    public const string VisitorRole = "user";

    /// <summary>How many members of staff are online right now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Distinct people. Zero when the count cannot be read.</returns>
    public Task<int> StaffOnlineAsync(CancellationToken cancellationToken)
        => staff.CountOnlineAsync(cancellationToken);

    /// <summary>Where one chat stands, as the visitor sees it.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The state, <c>bot</c> when nobody was ever asked for.</returns>
    public async Task<HandoffState> StateAsync(string conversationId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var row = await handoffs.LatestAsync(conversationId, cancellationToken).ConfigureAwait(false);

        return HandoffState.Of(row, await StaffOnlineAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Asks for a person on a chat. A chat already waiting or with a person gets its open row back,
    /// so a second tap of the button changes nothing.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="askedBy">Which side asked.</param>
    /// <param name="reason">What the person is for, when the asker said.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The open row, and whether this ask is the one that made it.</returns>
    public async Task<HandoffAsked> AskAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var wasOpen = await handoffs.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is not null;

        var row = await handoffs.AskAsync(conversationId, askedBy, reason, cancellationToken).ConfigureAwait(false);

        return new HandoffAsked(row, Created: !wasOpen);
    }

    /// <summary>Records where a reply goes when the visitor is not there to read it.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="email">The visitor's address.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the chat had an open handoff to put it on.</returns>
    public Task<bool> SetEmailAsync(string conversationId, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(email);

        return handoffs.SetEmailAsync(conversationId, email, cancellationToken);
    }

    /// <summary>Puts the visitor's words in a chat that is waiting or with a person.</summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="text">The words.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>
    /// The message as it was pushed, or <see langword="null"/> when the bot has the chat and the
    /// words belong on the chat route instead.
    /// </returns>
    public async Task<HandoffMessage?> VisitorSaysAsync(string conversationId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(text);

        if (await handoffs.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var at = clock.GetUtcNow();

        var row = await conversations
            .AppendMessageAsync(conversationId, new ChatMessage(ChatRole.User, text) { CreatedAt = at }, cancellationToken)
            .ConfigureAwait(false);

        var created = new HandoffMessage(conversationId, row.MessageId, VisitorRole, text, Speaker: null, at);

        await notifier.MessageCreatedAsync(created, cancellationToken).ConfigureAwait(false);

        return created;
    }
}
