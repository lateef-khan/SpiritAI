using System.Text.Json;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>One page of GoTo's call list.</summary>
/// <param name="Calls">The calls on it.</param>
/// <param name="NextPageMarker">The marker of the next page, or null on the last.</param>
public sealed record GoToReportSummaryPage(IReadOnlyList<GoToReportSummary> Calls, string? NextPageMarker)
{
    /// <summary>Reads one <c>GET /call-events-report/v1/report-summaries</c> answer.</summary>
    /// <param name="page">GoTo's answer.</param>
    /// <returns>The page.</returns>
    public static GoToReportSummaryPage Read(JsonElement page)
        => new([.. Items(page, "items").Select(GoToReportSummary.Read)], Text(page, "nextPageMarker") is { Length: > 0 } next ? next : null);
}
