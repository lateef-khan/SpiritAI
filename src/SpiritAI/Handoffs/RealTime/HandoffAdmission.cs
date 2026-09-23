using AgentCore.Application.Ports;

using SpiritAI.Contacts;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;

namespace SpiritAI.Handoffs.RealTime;

/// <summary>
/// Admits a visitor to the socket: they name a chat and their key, are checked to own that chat,
/// join its group, and may signal staff. Staff work in Chatwoot and never join. Anyone else is
/// left to another feature.
/// </summary>
public sealed class HandoffAdmission(
    IConversations conversations,
    IContactResolver contacts,
    IContactConversationStore contactConversations) : IRealTimeAdmission
{
    /// <summary>
    /// The kind a member of staff is counted and signals under. Nobody joins as staff now; the
    /// Chatwoot webhook signs its typing signals with it, and the widget listens for it.
    /// </summary>
    public const string StaffKind = "staff";

    /// <summary>The kind a visitor is counted under.</summary>
    public const string VisitorKind = "visitor";

    /// <summary>The query string field a visitor names their chat in.</summary>
    public const string ConversationQuery = "call";

    /// <summary>The query string field a visitor sends their key in.</summary>
    public const string VisitorQuery = "visitor";

    /// <inheritdoc />
    public ValueTask<RealTimeCaller?> AdmitAsync(RealTimeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return AdmitVisitorAsync(request.Query, cancellationToken);
    }

    private async ValueTask<RealTimeCaller?> AdmitVisitorAsync(IQueryCollection query, CancellationToken cancellationToken)
    {
        string conversationId = query[ConversationQuery].ToString();
        string visitor = query[VisitorQuery].ToString();

        if (conversationId.Length == 0 || !VisitorPrincipal.IsWellFormed(visitor))
        {
            return null;
        }

        var key = VisitorPrincipal.KeyOf(visitor);

        if (await ContactConversationOwnership.ReadAsync(conversations, contactConversations, contacts, conversationId, key, cancellationToken).ConfigureAwait(false)
            is null)
        {
            return null;
        }

        return new RealTimeCaller(
            key,
            Name: null,
            VisitorKind,
            [HandoffGroups.ForConversation(conversationId), HandoffGroups.Visitors],
            group => string.Equals(group, HandoffGroups.Staff, StringComparison.Ordinal));
    }
}
