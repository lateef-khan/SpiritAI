using System.Text.Json;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>One call in GoTo's call list.</summary>
/// <param name="Id">The <c>conversationSpaceId</c>.</param>
/// <param name="MayBeLogged">
/// Whether the call can be worth a note: inbound, it reached a staff phone; outbound, it dialed an
/// outside number. Only these are worth reading in full.
/// </param>
public sealed record GoToReportSummary(string Id, bool MayBeLogged)
{
    /// <summary>Reads one item of <c>GET /call-events-report/v1/report-summaries</c>.</summary>
    /// <param name="summary">The item.</param>
    /// <returns>The call.</returns>
    public static GoToReportSummary Read(JsonElement summary)
    {
        var participants = Items(summary, "participants").ToList();

        // Inbound, the participants are the staff phones the call reached; outbound, the parties dialed.
        var mayBeLogged = Text(summary, "direction") == "OUTBOUND"
            ? participants.Any(p => Text(p, "type", "value") == "PHONE_NUMBER")
            : participants.Count > 0;

        return new GoToReportSummary(Text(summary, "conversationSpaceId")!, mayBeLogged);
    }
}
