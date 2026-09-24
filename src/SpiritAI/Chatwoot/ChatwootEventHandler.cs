using System.Text.Json;

using AgentCore.Application.Ports;

using Microsoft.Extensions.AI;

using SpiritAI.Handoffs.Contracts;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.Notifications;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Store;
using SpiritAI.Handoffs.Transcript;
using SpiritAI.RealTime;
using SpiritAI.RealTime.Presence;
using SpiritAI.Threads;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Carries what staff do in Chatwoot to the visitor's widget: their words, that one of them has
/// the chat, that the chat is closed, and their typing. The handoff store still records who holds
/// a chat, because the visitor's routes read it.
/// </summary>
public sealed class ChatwootEventHandler(
    IConversations conversations,
    IHandoffStore handoffs,
    IHandoffNotifier notifier,
    IRealTimePublisher publisher,
    IPresenceStore presence,
    IHandoffMailer mailer,
    TimeProvider clock,
    ILogger<ChatwootEventHandler> logger)
{
    /// <summary>The team a staff reply is signed with, under the name.</summary>
    public const string StaffDetail = "Support";

    /// <summary>The name a staff reply is signed with when Chatwoot sends none.</summary>
    public const string UnnamedStaff = "Support";

    /// <summary>The role a staff reply is stored and pushed under.</summary>
    public const string StaffRole = "assistant";

    /// <summary>The signal the widget draws typing dots for.</summary>
    public const string TypingSignal = "typing";

    /// <summary>Does what one event asks.</summary>
    /// <param name="e">The event.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    public async Task HandleAsync(ChatwootEvent e, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(e);

        var action = ChatwootEventFilter.ActionOf(e);

        if (action == ChatwootAction.Ignore)
        {
            return;
        }

        var conversationId = e.SpiritConversationId!;

        switch (action)
        {
            case ChatwootAction.StaffMessage:
                await StaffSaysAsync(conversationId, e, cancellationToken).ConfigureAwait(false);
                break;

            case ChatwootAction.Assigned:
                await AssignedAsync(conversationId, e.AssigneeName!, cancellationToken).ConfigureAwait(false);
                break;

            case ChatwootAction.Resolved:
                if (await handoffs.DoneAsync(conversationId, cancellationToken).ConfigureAwait(false))
                {
                    await notifier.DoneAsync(conversationId, cancellationToken).ConfigureAwait(false);
                }

                break;

            case ChatwootAction.TypingOn or ChatwootAction.TypingOff:
                await TypingAsync(conversationId, e, action == ChatwootAction.TypingOn, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    private async Task StaffSaysAsync(string conversationId, ChatwootEvent e, CancellationToken cancellationToken)
    {
        var text = e.Content!;
        var name = e.ActorName ?? UnnamedStaff;
        var speaker = HandoffSpeaker.Human(name, StaffDetail);
        var at = clock.GetUtcNow();

        var message = new ChatMessage(ChatRole.Assistant, text) { CreatedAt = at };
        SpeakerProperty.Attach(message, speaker);

        var row = await conversations.AppendMessageAsync(conversationId, message, cancellationToken).ConfigureAwait(false);

        await notifier.MessageCreatedAsync(
            new HandoffMessage(conversationId, row.MessageId, StaffRole, text, speaker, at),
            cancellationToken).ConfigureAwait(false);

        var open = await handoffs.OpenAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if ((open?.Email ?? e.ContactEmail) is { } email
            && !await VisitorIsHereAsync(conversationId, cancellationToken).ConfigureAwait(false))
        {
            await MailAsync(new HandoffReplyMail(email, conversationId, name, text), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task AssignedAsync(string conversationId, string name, CancellationToken cancellationToken)
    {
        var key = StaffKey(name);

        var claim = await handoffs.ClaimAsync(conversationId, key, name, cancellationToken).ConfigureAwait(false);

        if (claim.Result == HandoffClaimResult.Won
            || (claim.Result == HandoffClaimResult.AlreadyTaken
                && await handoffs.HandOverAsync(conversationId, key, name, cancellationToken).ConfigureAwait(false)))
        {
            await notifier.ClaimedAsync(conversationId, new HandoffAssignee(key, name), cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask TypingAsync(string conversationId, ChatwootEvent e, bool on, CancellationToken cancellationToken)
    {
        var group = HandoffGroups.ForConversation(conversationId);
        var sender = new RealTimeSender(StaffKey(e.ActorId?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? UnnamedStaff), HandoffAdmission.StaffKind);
        var payload = JsonSerializer.SerializeToElement(new { callId = conversationId, on });

        return publisher.PublishAsync(group, RealTimeEvents.Signal, new RealTimeSignal(sender, group, TypingSignal, payload), cancellationToken);
    }

    /// <summary>The caller key a Chatwoot member of staff is filed under. They never sign in to Spirit.</summary>
    private static string StaffKey(string who) => "chatwoot:" + who;

    /// <summary>Whether the chat's owner has a socket open right now. A chat with no owner counts as away.</summary>
    private async Task<bool> VisitorIsHereAsync(string conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        return ThreadEnvelope.OwnerOf(conversation?.Custom) is { } owner
            && await presence.IsOnlineAsync(owner, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Mail is the bridge to a visitor who left; the words are already in the chat, so a miss costs a log line.</summary>
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
}
