using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.RealTime.Presence;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// The visitor's side of a handoff, section 5 of the spec: the ask, the wait, and the words said
/// while waiting. Both the public routes and the bot's own tool come through here, so a chat joins
/// the queue the same way whichever side asked.
/// </summary>
public sealed class HandoffDesk(
    IHandoffStore handoffs,
    ICallStore calls,
    IHandoffTranscript transcript,
    IHandoffNotifier notifier,
    IPresenceStore presence,
    TimeProvider clock)
{
    /// <summary>The role a visitor's message is stored and pushed under.</summary>
    public const string VisitorRole = "user";

    /// <summary>How many members of staff are on a socket right now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Distinct people, so two tabs of one person count once.</returns>
    public Task<int> StaffOnlineAsync(CancellationToken cancellationToken)
        => presence.CountOnlineAsync(HandoffAdmission.StaffKind, cancellationToken);

    /// <summary>Where one chat stands, as the visitor sees it.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The state, <c>bot</c> when nobody was ever asked for.</returns>
    public async Task<HandoffState> StateAsync(string callId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        var row = await handoffs.LatestAsync(callId, cancellationToken).ConfigureAwait(false);

        var position = row is { Status: HandoffStatus.Waiting }
            ? await handoffs.PositionAsync(callId, cancellationToken).ConfigureAwait(false)
            : null;

        return HandoffState.Of(row, position, await StaffOnlineAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Asks for a person on a chat. A chat already waiting or with a person gets its open row back
    /// and nothing is pushed, so a second tap of the button changes nothing.
    /// </summary>
    /// <param name="callId">The chat.</param>
    /// <param name="askedBy">Which side asked.</param>
    /// <param name="reason">What the person is for, when the asker said.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The ticket, and whether this ask is the one that made the row.</returns>
    public async Task<HandoffAsked> AskAsync(
        string callId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);

        // The store's ask is idempotent, so whether this one made the row is read before it: two
        // asks racing on one chat may both push, which a listener treats as the hint it is.
        var wasOpen = await handoffs.OpenAsync(callId, cancellationToken).ConfigureAwait(false) is not null;

        var ticket = await handoffs.AskAsync(callId, askedBy, reason, cancellationToken).ConfigureAwait(false);

        if (wasOpen)
        {
            return new HandoffAsked(ticket, Created: false);
        }

        var summary = await HandoffSummaries.OfAsync(handoffs, calls, ticket.Row, cancellationToken).ConfigureAwait(false);

        await notifier.WaitingAsync(summary, cancellationToken).ConfigureAwait(false);

        return new HandoffAsked(ticket, Created: true);
    }

    /// <summary>Records where a reply goes when the visitor is not there to read it.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="email">The visitor's address.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Whether the chat had an open handoff to put it on.</returns>
    public Task<bool> SetEmailAsync(string callId, string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentException.ThrowIfNullOrEmpty(email);

        return handoffs.SetEmailAsync(callId, email, cancellationToken);
    }

    /// <summary>Puts the visitor's words in a chat that is waiting or with a person.</summary>
    /// <param name="callId">The chat.</param>
    /// <param name="text">The words.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>
    /// The message as it was pushed, or <see langword="null"/> when the bot has the chat and the
    /// words belong on the chat route instead.
    /// </returns>
    /// <exception cref="NotSupportedException">AgentCore cannot yet append between turns.</exception>
    public async Task<HandoffMessage?> VisitorSaysAsync(string callId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(callId);
        ArgumentException.ThrowIfNullOrEmpty(text);

        if (await handoffs.OpenAsync(callId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var at = clock.GetUtcNow();

        await transcript
            .AppendAsync(callId, new ChatMessage(ChatRole.User, text) { CreatedAt = at }, cancellationToken)
            .ConfigureAwait(false);

        var created = new HandoffMessage(callId, VisitorRole, text, Speaker: null, at);

        await notifier.MessageCreatedAsync(created, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>
    /// Tells every waiting chat where it stands now. Called after a claim or a close moves the line.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pushes.</param>
    public async Task AnnounceQueueAsync(CancellationToken cancellationToken)
    {
        var waiting = await handoffs
            .ListAsync(HandoffStatus.Waiting, HandoffStore.MaxListSize, cancellationToken)
            .ConfigureAwait(false);

        for (var index = 0; index < waiting.Count; index++)
        {
            await notifier.QueueAsync(waiting[index].CallId, index + 1, cancellationToken).ConfigureAwait(false);
        }
    }
}
