using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Model;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.RealTime.Presence;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.Desk;

/// <summary>
/// The conversation of a handoff, section 5 of the spec: the ask, the wait, and the words either
/// side says while a person is on the way or on the chat. The public routes, the inbox's reply,
/// and the bot's own tool all come through here, so a chat joins the queue the same way whichever
/// side asked, and a reply reaches the visitor the same way wherever they are. Every word of the
/// human phase goes into the chat's own history through <see cref="IConversations.AppendMessageAsync"/>,
/// so the bot's next turn after Done reads the human phase: AgentCore re-reads the call when
/// <c>next_ordinal</c> moved.
/// </summary>
public sealed class HandoffDesk(
    IHandoffStore handoffs,
    IConversations conversations,
    IHandoffNotifier notifier,
    IPresenceStore presence,
    IHandoffMailer mailer,
    TimeProvider clock,
    ILogger<HandoffDesk> logger)
{
    /// <summary>The role a visitor's message is stored and pushed under.</summary>
    public const string VisitorRole = "user";

    /// <summary>The role a member of staff's reply is stored and pushed under.</summary>
    public const string StaffRole = "assistant";

    /// <summary>The team a staff reply is signed with, under the name.</summary>
    public const string StaffDetail = "Support";

    /// <summary>How many members of staff are on a socket right now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Distinct people, so two tabs of one person count once.</returns>
    public Task<int> StaffOnlineAsync(CancellationToken cancellationToken)
        => presence.CountOnlineAsync(HandoffAdmission.StaffKind, cancellationToken);

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
    /// Asks for a person on a chat. A chat already waiting or with a person gets its open row back
    /// and nothing is pushed, so a second tap of the button changes nothing.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="askedBy">Which side asked.</param>
    /// <param name="reason">What the person is for, when the asker said.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>The ticket, and whether this ask is the one that made the row.</returns>
    public async Task<HandoffAsked> AskAsync(
        string conversationId, HandoffAskedBy askedBy, string? reason, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        // The store's ask is idempotent, so whether this one made the row is read before it: two
        // asks racing on one chat may both push, which a listener treats as the hint it is.
        var wasOpen = await handoffs.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false) is not null;

        var ticket = await handoffs.AskAsync(conversationId, askedBy, reason, cancellationToken).ConfigureAwait(false);

        if (wasOpen)
        {
            return new HandoffAsked(ticket, Created: false);
        }

        var summary = await HandoffSummaries.OfAsync(handoffs, conversations, ticket.Row, seenOrdinal: null, cancellationToken).ConfigureAwait(false);

        await notifier.WaitingAsync(summary, cancellationToken).ConfigureAwait(false);

        return new HandoffAsked(ticket, Created: true);
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

    /// <summary>
    /// Writes one of the host's own lines, "Dana joined" or "Dana left", into a chat, and pushes it
    /// the way any other message of the human phase is pushed: both screens draw it the moment it
    /// is written, not the next time they read the history.
    /// </summary>
    /// <param name="conversationId">The chat.</param>
    /// <param name="text">The line.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The message as it was pushed.</returns>
    public async Task<HandoffMessage> NoteAsync(string conversationId, string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentException.ThrowIfNullOrEmpty(text);

        var speaker = HandoffSpeaker.System();
        var at = clock.GetUtcNow();
        var line = new ChatMessage(ChatRole.Assistant, text) { CreatedAt = at };
        SpeakerProperty.Attach(line, speaker);

        var row = await conversations.AppendMessageAsync(conversationId, line, cancellationToken).ConfigureAwait(false);

        var created = new HandoffMessage(conversationId, row.MessageId, StaffRole, text, speaker, at);

        await notifier.MessageCreatedAsync(created, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <summary>
    /// Puts a member of staff's words in the chat they hold, and mails them on when the visitor is
    /// not there to read them.
    /// </summary>
    /// <param name="open">The chat's open row. The caller has checked it is this member's.</param>
    /// <param name="member">Who is replying.</param>
    /// <param name="text">The words.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The message as it was pushed.</returns>
    public async Task<HandoffMessage> StaffSaysAsync(
        Handoff open, HandoffStaffMember member, string text, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentException.ThrowIfNullOrEmpty(text);

        var speaker = HandoffSpeaker.Human(member.Name, StaffDetail);
        var at = clock.GetUtcNow();
        var message = new ChatMessage(ChatRole.Assistant, text) { CreatedAt = at };
        SpeakerProperty.Attach(message, speaker);

        var row = await conversations.AppendMessageAsync(open.ConversationId, message, cancellationToken).ConfigureAwait(false);

        var created = new HandoffMessage(open.ConversationId, row.MessageId, StaffRole, text, speaker, at);

        await notifier.MessageCreatedAsync(created, cancellationToken).ConfigureAwait(false);

        if (open.Email is { } email && !await VisitorIsHereAsync(open.ConversationId, cancellationToken).ConfigureAwait(false))
        {
            await MailAsync(new HandoffReplyMail(email, open.ConversationId, member.Name, text), cancellationToken)
                .ConfigureAwait(false);
        }

        return created;
    }

    /// <summary>Whether the chat's owner has a socket open right now.</summary>
    /// <remarks>
    /// A chat with no owner on record has nobody who could be here, so it counts as away and the
    /// mail goes: the address on the row is the only way to reach whoever left it.
    /// </remarks>
    private async Task<bool> VisitorIsHereAsync(string conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        return ThreadEnvelope.OwnerOf(conversation?.Custom) is { } owner
            && await presence.IsOnlineAsync(owner, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Sends the reply on by mail, and swallows a miss.</summary>
    /// <remarks>
    /// By now the words are in the chat and on the socket. Mail is the bridge to a visitor who
    /// left, not the record of what was said, so a bridge that is down costs a log line and never
    /// the reply.
    /// </remarks>
    private async Task MailAsync(HandoffReplyMail mail, CancellationToken cancellationToken)
    {
        try
        {
            await mailer.SendReplyAsync(mail, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "The reply on conversation {ConversationId} was not mailed.", mail.ConversationId);
        }
    }

    /// <summary>
    /// Tells every waiting chat where it stands now. Called after a claim or a close moves the line.
    /// </summary>
    /// <param name="cancellationToken">Cancels the pushes.</param>
    public async Task AnnounceQueueAsync(CancellationToken cancellationToken)
    {
        var position = 0;
        HandoffCursor? after = null;
        HandoffFilter queue = new(HandoffView.Waiting);

        do
        {
            var page = await handoffs
                .ListAsync(queue, HandoffStore.MaxListSize, after, cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in page.Rows)
            {
                await notifier.QueueAsync(row.ConversationId, ++position, cancellationToken).ConfigureAwait(false);
            }

            after = page.Next;
        }
        while (after is not null);
    }
}
