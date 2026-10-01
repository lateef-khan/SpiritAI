using System.Text.Json;
using System.Text.Json.Nodes;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>Real September call reports, as <c>call_log_fixtures.py</c> saved them.</summary>
public sealed class GoToCallReportTests
{
    [Fact]
    public void ATransferListsEveryStaffPhoneWithWhenItRangAndAnswered()
    {
        var report = GoToCallReport.Read(GoToPayloads.Read("report_transfer"));

        Assert.Equal("d51fe878-4589-3812-b625-6895cdbd83be", report.Id);
        Assert.False(report.Outbound);
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T18:38:14.189Z"), report.Created);
        Assert.Equal(DateTimeOffset.Parse("2026-09-30T18:44:34.805Z"), report.Ended);
        Assert.Equal(new GoToOutsideParty("+12015550100", "GRAND PR     TX", DateTimeOffset.Parse("2026-09-30T18:38:43.969Z")), report.Outside);
        Assert.Equal("+18002584555", report.CompanyLine);
        Assert.Equal("Service - Problems", report.QueueName);
        Assert.False(report.QueueAbandoned);
        Assert.False(report.Voicemail);
        Assert.Equal(
            [
                new GoToReportStaff("Test Person 8648", "8648", DateTimeOffset.Parse("2026-09-30T18:38:36.213Z"), DateTimeOffset.Parse("2026-09-30T18:38:43.969Z")),
                new GoToReportStaff("Test Person 8621", "8621", DateTimeOffset.Parse("2026-09-30T18:38:36.217Z"), null),
                new GoToReportStaff("Test Person 8614", "8614", DateTimeOffset.Parse("2026-09-30T18:39:25.665Z"), DateTimeOffset.Parse("2026-09-30T18:39:28.780Z")),
            ],
            report.Staff);
    }

    [Fact]
    public void OnePhoneThatShowsUpTwiceIsOnePersonWithTheEarliestRing()
    {
        var report = GoToCallReport.Read(GoToPayloads.Read("report_missed"));

        var person = Assert.Single(report.Staff);
        Assert.Equal(new GoToReportStaff("Test Person 8615", "8615", DateTimeOffset.Parse("2026-09-28T16:29:29.642Z"), null), person);
    }

    [Fact]
    public void AFailedVoicemailIsNotAVoicemail()
        => Assert.False(GoToCallReport.Read(GoToPayloads.Read("report_missed")).Voicemail);

    [Fact]
    public void AVoicemailThatTookAMessageIsAVoicemail()
        => Assert.True(GoToCallReport.Read(GoToPayloads.Read("report_answered_then_voicemail")).Voicemail);

    [Fact]
    public void ACallerWhoHungUpInTheQueueIsMarked()
    {
        var report = GoToCallReport.Read(GoToPayloads.Read("report_queue_abandon"));

        Assert.True(report.QueueAbandoned);
        Assert.Equal("Sales", report.QueueName);
    }

    [Fact]
    public void AnOutboundCallsOutsidePartyIsTheNumberDialed()
    {
        var report = GoToCallReport.Read(GoToPayloads.Read("report_outbound_answered"));

        Assert.True(report.Outbound);
        Assert.Equal(new GoToOutsideParty("+12015550100", null, DateTimeOffset.Parse("2026-09-30T18:07:59.311Z")), report.Outside);
        Assert.Equal("+18709351107", report.CompanyLine);
    }

    [Fact]
    public void AStaffToStaffCallHasNoOutsideParty()
        => Assert.Null(GoToCallReport.Read(GoToPayloads.Read("report_staff_to_staff")).Outside);

    [Fact]
    public void AHiddenCallerHasNoOutsidePartyButKeepsTheCompanyLine()
    {
        var report = GoToCallReport.Read(GoToPayloads.Read("report_anonymous"));

        Assert.Null(report.Outside);
        Assert.Equal("+18002584555", report.CompanyLine);
    }

    [Theory]
    [InlineData("callCreated")]
    [InlineData("callEnded")]
    public void AReportWithoutACallTimeNamesTheFieldAndTheCall(string field)
    {
        var exception = Assert.Throws<FormatException>(() => GoToCallReport.Read(Without("report_answered", field)));

        Assert.Contains(field, exception.Message, StringComparison.Ordinal);
        Assert.Contains(AnsweredId, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AReportWithoutACallIdNamesTheField()
    {
        var exception = Assert.Throws<FormatException>(() => GoToCallReport.Read(Without("report_answered", "conversationSpaceId")));

        Assert.Contains("conversationSpaceId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ACallStateWithoutATimestampNamesTheFieldAndTheCall()
    {
        var report = JsonNode.Parse(GoToPayloads.Read("report_answered").GetRawText())!.AsObject();
        report["callStates"]!.AsArray()[1]!.AsObject().Remove("timestamp");

        var exception = Assert.Throws<FormatException>(() => GoToCallReport.Read(JsonDocument.Parse(report.ToJsonString()).RootElement));

        Assert.Contains("timestamp", exception.Message, StringComparison.Ordinal);
        Assert.Contains(AnsweredId, exception.Message, StringComparison.Ordinal);
    }

    private const string AnsweredId = "84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private static JsonElement Without(string payload, string field)
    {
        var report = JsonNode.Parse(GoToPayloads.Read(payload).GetRawText())!.AsObject();
        report.Remove(field);

        return JsonDocument.Parse(report.ToJsonString()).RootElement;
    }
}
