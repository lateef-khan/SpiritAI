using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Caching;
using SpiritAI.Chatwoot;
using SpiritAI.GoTo;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Callback;

/// <summary>
/// When staff dial a number a visitor left for a call back, gives the waiting conversation to
/// whoever dialed.
/// </summary>
public sealed class CallbackCalled(
    ChatwootClient chatwoot,
    ChatwootConversationTags tags,
    CallStaff staff,
    HybridCache cache,
    ILogger<CallbackCalled> logger) : IGoToCallHandler
{
    private const string Done = "done";

    /// <summary>
    /// Shared through the second level, so another server skips a call this one handled. GoTo sends
    /// several events for one call.
    /// </summary>
    private static readonly HybridCacheEntryOptions DoneEntry = new()
    {
        Expiration = CallRingAlert.SeenFor,
        LocalCacheExpiration = CallRingAlert.SeenFor,
    };

    /// <inheritdoc />
    public async Task HandleAsync(GoToCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        if (!call.Outbound || call.OutsideNumber is not { } number)
        {
            return;
        }

        var key = $"goto:calledback:{call.Id}";

        if (await cache.PeekAsync<string>(key, cancellationToken).ConfigureAwait(false) is not null)
        {
            return;
        }

        if (await CallerAsync(call, cancellationToken).ConfigureAwait(false) is { } caller)
        {
            var waiting = await tags.FindAsync(CallbackQueue.PhoneField, number, CallbackQueue.Label, cancellationToken)
                .ConfigureAwait(false);

            foreach (var conversationId in waiting)
            {
                await chatwoot.AssignAgentAsync(conversationId, caller.Id, cancellationToken).ConfigureAwait(false);

                logger.LogInformation(
                    "Call {ConversationSpaceId} dialed the call back of conversation {ConversationId}; assigned to agent {AgentId}.",
                    call.Id,
                    conversationId,
                    caller.Id);
            }
        }

        await cache.SetAsync(key, Done, DoneEntry, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<ChatwootAgent?> CallerAsync(GoToCall call, CancellationToken cancellationToken)
    {
        foreach (var line in call.Lines)
        {
            if (await staff.FindAgentAsync(line, cancellationToken).ConfigureAwait(false) is { } agent)
            {
                return agent;
            }
        }

        return null;
    }
}
