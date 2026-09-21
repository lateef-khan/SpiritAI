using System.Security.Claims;

using AgentCore.Application.Ports;

using SpiritAI.Handoffs.Staff;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;
using SpiritAI.Threads;

namespace SpiritAI.Handoffs.RealTime;

/// <summary>
/// Admits the two callers of section 6.1 of the handoff spec. A signed-in member of staff joins
/// the staff group and may signal any chat. A visitor names a chat and their key, is checked to
/// own that chat, joins its group, and may signal staff. Anyone else is left to another feature.
/// </summary>
public sealed class HandoffAdmission(IConversations conversations, StaffGate staff) : IRealTimeAdmission
{
    /// <summary>The kind a member of staff is counted under.</summary>
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

        return request.User is { } user
            ? AdmitStaffAsync(user, cancellationToken)
            : AdmitVisitorAsync(request.Query, cancellationToken);
    }

    /// <summary>
    /// A signed-in caller the gate knows. One it does not gets <see langword="null"/>, not a
    /// refusal: another feature may still know them.
    /// </summary>
    private async ValueTask<RealTimeCaller?> AdmitStaffAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (CallerPrincipal.KeyOf(user) is not { } key)
        {
            return null;
        }

        var member = await staff.MemberOfAsync(user, cancellationToken).ConfigureAwait(false);

        if (member is null)
        {
            return null;
        }

        return new RealTimeCaller(key, member.Name, StaffKind, [HandoffGroups.Staff], HandoffGroups.IsConversation);
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

        if (await ThreadOwnership.ReadAsync(conversations, conversationId, key, cancellationToken).ConfigureAwait(false) is null)
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
