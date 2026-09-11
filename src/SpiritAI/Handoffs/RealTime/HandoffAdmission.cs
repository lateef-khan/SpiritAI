using System.Security.Claims;

using AgentCore.Application.Ports;

using Microsoft.Extensions.Options;

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
public sealed class HandoffAdmission(ICallStore calls, IOptions<HandoffOptions> options) : IRealTimeAdmission
{
    /// <summary>The kind a member of staff is counted under.</summary>
    public const string StaffKind = "staff";

    /// <summary>The kind a visitor is counted under.</summary>
    public const string VisitorKind = "visitor";

    /// <summary>The query string field a visitor names their chat in.</summary>
    public const string CallQuery = "call";

    /// <summary>The query string field a visitor sends their key in.</summary>
    public const string VisitorQuery = "visitor";

    /// <inheritdoc />
    public ValueTask<RealTimeCaller?> AdmitAsync(RealTimeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.User is { } user
            ? ValueTask.FromResult(AdmitStaff(user))
            : AdmitVisitorAsync(request.Query, cancellationToken);
    }

    /// <summary>
    /// A signed-in caller who is on the staff list. One who is not gets <see langword="null"/>,
    /// not a refusal: a dealer is not staff, but another feature may still know them.
    /// </summary>
    private RealTimeCaller? AdmitStaff(ClaimsPrincipal user)
    {
        var member = StaffGate.MemberOf(user, options.Value);
        var key = CallerPrincipal.KeyOf(user);

        if (member is null || key is null)
        {
            return null;
        }

        return new RealTimeCaller(key, member.Name, StaffKind, [HandoffGroups.Staff], HandoffGroups.IsCall);
    }

    private async ValueTask<RealTimeCaller?> AdmitVisitorAsync(IQueryCollection query, CancellationToken cancellationToken)
    {
        string callId = query[CallQuery].ToString();
        string visitor = query[VisitorQuery].ToString();

        if (callId.Length == 0 || !VisitorPrincipal.IsWellFormed(visitor))
        {
            return null;
        }

        var key = VisitorPrincipal.KeyOf(visitor);

        if (await ThreadOwnership.ReadAsync(calls, callId, key, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        return new RealTimeCaller(
            key,
            Name: null,
            VisitorKind,
            [HandoffGroups.ForCall(callId), HandoffGroups.Visitors],
            group => string.Equals(group, HandoffGroups.Staff, StringComparison.Ordinal));
    }
}
