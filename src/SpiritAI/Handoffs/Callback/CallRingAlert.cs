using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

using SpiritAI.Caching;
using SpiritAI.Chatwoot;
using SpiritAI.GoTo;
using SpiritAI.Handoffs.Bot;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Callback;

/// <summary>
/// When a staff phone rings with a visitor who asked for a person, posts a private note in the
/// visitor's open Chatwoot conversation that mentions whoever's phone it is.
/// </summary>
public sealed class CallRingAlert(
    ChatwootClient chatwoot,
    CallStaff staff,
    HybridCache cache,
    IOptions<CallbackOptions> options,
    ILogger<CallRingAlert> logger) : IGoToCallHandler
{
    /// <summary>How long a line in a call is remembered as done. GoTo sends a ring on two or more events.</summary>
    public static readonly TimeSpan SeenFor = TimeSpan.FromHours(2);

    private const string Told = "told";

    /// <summary>Shared through the second level, so another server skips a line this one told about.</summary>
    private static readonly HybridCacheEntryOptions SeenEntry = new()
    {
        Expiration = SeenFor,
        LocalCacheExpiration = SeenFor,
    };

    /// <summary>
    /// The lines to tell about a call. Inbound, the ones ringing. Outbound, every staff line: the
    /// caller's own line never rings, it goes straight to <c>CONNECTED</c>.
    /// </summary>
    /// <param name="call">The call, as one event shows it.</param>
    /// <returns>The lines.</returns>
    public static IEnumerable<GoToCallLine> LinesToTell(GoToCall call)
    {
        ArgumentNullException.ThrowIfNull(call);

        return call.Outbound ? call.Lines : call.Lines.Where(l => l.Ringing);
    }

    /// <inheritdoc />
    public async Task HandleAsync(GoToCall call, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);

        var lines = new List<GoToCallLine>();

        foreach (var line in LinesToTell(call))
        {
            if (await cache.PeekAsync<string>(SeenKey(call, line), cancellationToken).ConfigureAwait(false) is null)
            {
                lines.Add(line);
            }
        }

        if (lines.Count == 0)
        {
            return;
        }

        if (call.OutsideNumber is { } number
            && await FindOpenConversationAsync(number, cancellationToken).ConfigureAwait(false) is { } conversation)
        {
            var mentions = await MentionsAsync(lines, conversation, cancellationToken).ConfigureAwait(false);
            var note = CallRingNote.Write(call.Outbound, VisitorPhone.Display(number), conversation.Id, mentions);

            await chatwoot.PostNoteAsync(conversation.Id, note, sourceId: null, cancellationToken).ConfigureAwait(false);

            logger.LogInformation(
                "Posted the ring note for call {ConversationSpaceId} in conversation {ConversationId} with {Mentions} mentions.",
                call.Id,
                conversation.Id,
                mentions.Count);
        }

        foreach (var line in lines)
        {
            await cache.SetAsync(SeenKey(call, line), Told, SeenEntry, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }

    private static string SeenKey(GoToCall call, GoToCallLine line) => $"goto:rang:{call.Id}:{line.LineId}";

    /// <summary>The newest open conversation of the contact with the number.</summary>
    private async Task<ChatwootContactConversation?> FindOpenConversationAsync(string e164, CancellationToken cancellationToken)
    {
        if (await chatwoot.FindPhoneContactAsync(e164, cancellationToken).ConfigureAwait(false) is not { } contact)
        {
            return null;
        }

        return (await chatwoot.ListContactConversationsAsync(contact.Id, cancellationToken).ConfigureAwait(false))
            .Where(c => c.Open)
            .MaxBy(c => c.LastActivityAt);
    }

    /// <summary>
    /// The staff whose line it is, matched from their GoTo email to their Chatwoot email. When none
    /// match, the conversation's team, and then the fallback team.
    /// </summary>
    private async Task<IReadOnlyList<string>> MentionsAsync(
        IReadOnlyList<GoToCallLine> lines, ChatwootContactConversation conversation, CancellationToken cancellationToken)
    {
        var mentioned = new List<ChatwootAgent>();

        foreach (var line in lines)
        {
            if (await staff.FindAgentAsync(line, cancellationToken).ConfigureAwait(false) is { } agent
                && !mentioned.Contains(agent))
            {
                mentioned.Add(agent);
            }
        }

        if (mentioned.Count > 0)
        {
            return [.. mentioned.Select(CallRingNote.Mention)];
        }

        var team = conversation.Team ?? await FallbackTeamAsync(cancellationToken).ConfigureAwait(false);

        return team is null ? [] : [CallRingNote.Mention(team)];
    }

    private async Task<ChatwootTeam?> FallbackTeamAsync(CancellationToken cancellationToken)
    {
        if (options.Value.FallbackTeamId is not { } teamId)
        {
            return null;
        }

        var teams = await chatwoot.ListTeamsAsync(cancellationToken).ConfigureAwait(false);

        return teams.FirstOrDefault(t => t.Id == teamId);
    }
}
