using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.GoTo;

namespace SpiritAI.CallLog;

/// <summary>
/// Copies one GoTo call into Chatwoot.
/// </summary>
public sealed class CallLogCopier(
    GoToClient goTo,
    GoToCompanyLines lines,
    ChatwootClient chatwoot,
    IOptions<CallLogOptions> options,
    TimeProvider clock,
    ILogger<CallLogCopier> logger)
{
    /// <summary>
    /// How far GoTo's clock and Chatwoot's may disagree.
    /// </summary>
    private static readonly TimeSpan ClockDrift = TimeSpan.FromHours(1);

    /// <summary>Copies one call.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>What it did.</returns>
    public async Task<CallLogResult> CopyAsync(string callId, CancellationToken cancellationToken)
    {
        if (await SearchFindsNoteAsync(callId, cancellationToken).ConfigureAwait(false))
        {
            return CallLogResult.AlreadyCopied;
        }

        if (await ReadWhenReadyAsync(callId, cancellationToken).ConfigureAwait(false) is not { } report)
        {
            return CallLogResult.NotReady;
        }

        if (!CallLogNote.IsWorthLogging(report))
        {
            return CallLogResult.Skipped;
        }

        var number = report.Outside!.Number;

        var contact = await chatwoot.FindPhoneContactAsync(number, cancellationToken).ConfigureAwait(false)
            ?? await chatwoot.CreatePhoneContactAsync(CallLogNote.Phone(number), number, cancellationToken).ConfigureAwait(false);

        var conversationId = await chatwoot.FindInboxConversationAsync(contact.Id, cancellationToken).ConfigureAwait(false)
            ?? await StartConversationAsync(callId, contact, cancellationToken).ConfigureAwait(false);

        var sourceId = CallLogNote.SourceId(callId);

        if (await chatwoot
                .HasNoteAsync(conversationId, sourceId, CallLogNote.Marker(callId), report.Created - ClockDrift, cancellationToken)
                .ConfigureAwait(false))
        {
            return CallLogResult.AlreadyCopied;
        }

        var brand = report.CompanyLine is { } line ? await FindBrandAsync(callId, line, cancellationToken).ConfigureAwait(false) : null;

        await chatwoot.PostNoteAsync(conversationId, CallLogNote.Write(report, brand), sourceId, cancellationToken).ConfigureAwait(false);

        return CallLogResult.Copied;
    }

    /// <summary>
    /// The search only saves reading the call from GoTo; <see cref="ChatwootClient.HasNoteAsync"/> still
    /// stops a second note, so a search that fails is no reason to stop the copy.
    /// </summary>
    private async Task<bool> SearchFindsNoteAsync(string callId, CancellationToken cancellationToken)
    {
        try
        {
            return await chatwoot.HasCallNoteAsync(CallLogNote.Marker(callId), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Call log could not search for call {ConversationSpaceId}'s note; the conversation is checked instead.", callId);

            return false;
        }
    }

    /// <summary>
    /// Starts the call's conversation, and resolves it only when Chatwoot really created it.
    /// </summary>
    private async Task<int> StartConversationAsync(string callId, ChatwootPhoneContact contact, CancellationToken cancellationToken)
    {
        var created = await chatwoot
            .CreateConversationAsync(
                contact.Id,
                contact.SourceId ?? await chatwoot.AddContactToInboxAsync(contact.Id, cancellationToken).ConfigureAwait(false),
                cancellationToken)
            .ConfigureAwait(false);

        var conversationId = created.Id;

        if (!created.Fresh)
        {
            logger.LogWarning(
                "Chatwoot answered the create with an existing conversation; not resolving it. Conversation {ConversationId}, call {ConversationSpaceId}.", conversationId, callId);

            return conversationId;
        }

        Exception? failure = null;

        foreach (var wait in options.Value.EffectiveResolveWaits.Prepend(TimeSpan.Zero))
        {
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, clock, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                await chatwoot.ResolveConversationAsync(conversationId, cancellationToken).ConfigureAwait(false);

                return conversationId;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                failure = exception;
            }
        }

        logger.LogWarning(failure, "Call log could not resolve conversation {ConversationId} for call {ConversationSpaceId}; its note is posted anyway.", conversationId, callId);

        return conversationId;
    }

    /// <summary>The brand only dresses the note, so a failed read of GoTo's lines leaves it out.</summary>
    private async Task<string?> FindBrandAsync(string callId, string line, CancellationToken cancellationToken)
    {
        try
        {
            return await lines.FindBrandAsync(line, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "Call log found no brand for call {ConversationSpaceId}; its note goes without one.", callId);
            return null;
        }
    }

    /// <summary>A live event can come before GoTo has the report, so a missing one is asked for again.</summary>
    private async Task<GoToCallReport?> ReadWhenReadyAsync(string callId, CancellationToken cancellationToken)
    {
        foreach (var wait in options.Value.EffectiveReportWaits.Prepend(TimeSpan.Zero))
        {
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, clock, cancellationToken).ConfigureAwait(false);
            }

            if (await goTo.ReadReportAsync(callId, cancellationToken).ConfigureAwait(false) is { } report)
            {
                return report;
            }
        }

        return null;
    }
}
