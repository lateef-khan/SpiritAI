using SpiritAI.CallLog;
using SpiritAI.GoTo;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.CallLog;

/// <summary>The note each real September call gets, per the spec's section 5.</summary>
public sealed class CallLogNoteTests
{
    [Fact]
    public void AnAnsweredCall()
        => Assert.Equal(
            """
            📞 Inbound call · Answered by Test Person 8625 (8625)
            Line: Spirit +1 800-258-8511 → Service - Premium
            Caller ID: PROBE CALLER, +1 201-555-0100
            Thu Sep 24, 2026, 12:47 PM CDT · waited 0:44 · talked 6:14
            GoTo call 84dcee04-ba4d-33c5-b00b-c0e57599fa69
            """.ReplaceLineEndings("\n"),
            Note("report_answered", "Spirit"));

    [Fact]
    public void ATransferNamesEveryoneWhoAnsweredInOrder()
        => Assert.Equal(
            """
            📞 Inbound call · Answered by Test Person 8648 (8648), then Test Person 8614 (8614)
            Line: Spirit +1 800-258-4555 → Service - Problems
            Caller ID: GRAND PR TX, +1 201-555-0100
            Wed Sep 30, 2026, 1:38 PM CDT · waited 0:29 · talked 5:50
            GoTo call d51fe878-4589-3812-b625-6895cdbd83be
            """.ReplaceLineEndings("\n"),
            Note("report_transfer", "Spirit"));

    [Fact]
    public void AnAnsweredCallThatEndedInVoicemail()
        => Assert.Equal(
            """
            📞 Inbound call · Answered by Test Person 8633 (8633) · then voicemail
            Line: Spirit +1 870-935-1107 → Operator
            Caller ID: PROBE CALLER, +1 201-555-0100
            Wed Sep 30, 2026, 10:52 AM CDT · waited 0:49 · talked 1:21
            GoTo call b6ad7073-be3a-31d5-a956-777109126542
            """.ReplaceLineEndings("\n"),
            Note("report_answered_then_voicemail", "Spirit"));

    [Fact]
    public void ACallerWhoHungUpInTheQueue()
        => Assert.Equal(
            """
            📞 Inbound call · Caller hung up in queue · rang Test Person 8623 (8623), Test Person 8629 (8629), Test Person 8617 (8617)
            Line: Spirit +1 800-258-8511 → Sales
            Caller ID: PROBE CALLER, +1 201-555-0100
            Fri Sep 25, 2026, 11:43 AM CDT
            GoTo call 7d880ac4-8751-387f-8d36-a25e657237cd
            """.ReplaceLineEndings("\n"),
            Note("report_queue_abandon", "Spirit"));

    [Fact]
    public void AMissedCall()
        => Assert.Equal(
            """
            📞 Inbound call · Missed · rang Test Person 8615 (8615)
            Line: Spirit +1 870-935-1107
            Caller ID: PROBE CALLER, +1 201-555-0100
            Mon Sep 28, 2026, 11:29 AM CDT
            GoTo call 2c0be098-e3ea-3416-a589-4c0c4ccc4477
            """.ReplaceLineEndings("\n"),
            Note("report_missed", "Spirit"));

    [Fact]
    public void AVoicemailNobodyAnswered()
    {
        // No September call went to voicemail without a staff answer first, so the missed call
        // stands in with its voicemail box marked as having taken a message.
        var missed = GoToCallReport.Read(GoToPayloads.Read("report_missed"));

        var note = CallLogNote.Write(missed with { Voicemail = true }, "Spirit");

        Assert.StartsWith("📞 Inbound call · Voicemail · rang Test Person 8615 (8615)\n", note);
    }

    [Fact]
    public void AnAnsweredOutboundCall()
        => Assert.Equal(
            """
            📞 Outbound call by Test Person 0019 (0019) · Answered
            Line: Spirit +1 870-935-1107
            Wed Sep 30, 2026, 1:07 PM CDT · talked 4:17
            GoTo call 02085621-dc03-3ebe-99d2-01f363be5dd4
            """.ReplaceLineEndings("\n"),
            Note("report_outbound_answered", "Spirit"));

    [Fact]
    public void AnOutboundCallIsByTheStaffWhoRangNotByOneWhoNeverDid()
    {
        var outbound = GoToCallReport.Read(GoToPayloads.Read("report_outbound_answered"));
        var neverRang = new GoToReportStaff("Test Person 0001", "0001", FirstRinging: null, FirstConnected: null);

        var note = CallLogNote.Write(outbound with { Staff = [neverRang, .. outbound.Staff] }, "Spirit");

        Assert.StartsWith("📞 Outbound call by Test Person 0019 (0019) · Answered\n", note);
    }

    [Fact]
    public void AnOutboundCallWhereNoStaffHasATimeIsByTheFirstStaffListed()
    {
        var outbound = GoToCallReport.Read(GoToPayloads.Read("report_outbound_answered"));
        var first = new GoToReportStaff("Test Person 0001", "0001", FirstRinging: null, FirstConnected: null);
        var second = new GoToReportStaff("Test Person 0002", "0002", FirstRinging: null, FirstConnected: null);

        var note = CallLogNote.Write(outbound with { Staff = [first, second] }, "Spirit");

        Assert.StartsWith("📞 Outbound call by Test Person 0001 (0001) · Answered\n", note);
    }

    [Fact]
    public void AnOutboundCallWithNoStaffAtAllHasNoCaller()
    {
        var outbound = GoToCallReport.Read(GoToPayloads.Read("report_outbound_answered"));

        var note = CallLogNote.Write(outbound with { Staff = [] }, "Spirit");

        Assert.StartsWith("📞 Outbound call · Answered\n", note);
    }

    [Fact]
    public void AnUnansweredOutboundCallWithNoStaffAtAllHasNoCaller()
    {
        var outbound = GoToCallReport.Read(GoToPayloads.Read("report_outbound_no_answer"));

        var note = CallLogNote.Write(outbound with { Staff = [] }, "Spirit");

        Assert.StartsWith("📞 Outbound call · No answer\n", note);
    }

    [Fact]
    public void AMissedCallThatRangNobodyNamesNobody()
    {
        var missed = GoToCallReport.Read(GoToPayloads.Read("report_missed"));

        var note = CallLogNote.Write(missed with { Staff = [] }, "Spirit");

        Assert.StartsWith("📞 Inbound call · Missed\nLine:", note);
    }

    [Fact]
    public void AnUnansweredOutboundCall()
        => Assert.Equal(
            """
            📞 Outbound call by Test Person 0014 (0014) · No answer
            Line: Spirit +1 870-935-1107
            Wed Sep 30, 2026, 12:24 PM CDT
            GoTo call b4e8e87f-7916-3ec6-9054-57bb8fbbfc98
            """.ReplaceLineEndings("\n"),
            Note("report_outbound_no_answer", "Spirit"));

    [Fact]
    public void ALineWithNoBrandShowsItsNumberOnly()
        => Assert.Contains("\nLine: +1 870-935-1107\n", Note("report_missed", brand: null));

    [Theory]
    [InlineData("report_answered")]
    [InlineData("report_transfer")]
    [InlineData("report_queue_abandon")]
    [InlineData("report_missed")]
    [InlineData("report_outbound_answered")]
    [InlineData("report_outbound_no_answer")]
    public void ACallThatReachedStaffIsLogged(string payload)
        => Assert.True(CallLogNote.IsWorthLogging(GoToCallReport.Read(GoToPayloads.Read(payload))));

    [Fact]
    public void ACallThatHungUpInTheMenuIsNotLogged()
        => Assert.False(CallLogNote.IsWorthLogging(GoToCallReport.Read(GoToPayloads.Read("report_menu_hangup"))));

    [Fact]
    public void AStaffToStaffCallIsNotLogged()
        => Assert.False(CallLogNote.IsWorthLogging(GoToCallReport.Read(GoToPayloads.Read("report_staff_to_staff"))));

    [Fact]
    public void ACallFromAHiddenNumberIsNotLogged()
        => Assert.False(CallLogNote.IsWorthLogging(GoToCallReport.Read(GoToPayloads.Read("report_anonymous"))));

    [Fact]
    public void TheNoteIsFoundAgainByTheCallId()
    {
        Assert.Equal("goto:84dcee04-ba4d-33c5-b00b-c0e57599fa69", CallLogNote.SourceId("84dcee04-ba4d-33c5-b00b-c0e57599fa69"));
        Assert.EndsWith(CallLogNote.Marker("84dcee04-ba4d-33c5-b00b-c0e57599fa69"), Note("report_answered", "Spirit"));
    }

    [Theory]
    [InlineData("report_anonymous")]
    [InlineData("report_menu_hangup")]
    [InlineData("report_staff_to_staff")]
    [InlineData("report_answered_then_voicemail")]
    public void AnyReportCanBeWrittenWhetherOrNotItIsWorthLogging(string payload)
        => Assert.EndsWith("\nGoTo call " + GoToCallReport.Read(GoToPayloads.Read(payload)).Id, Note(payload, brand: null));

    [Fact]
    public void ACallInJanuaryShowsCentralStandardTime()
    {
        var answered = GoToCallReport.Read(GoToPayloads.Read("report_answered"));
        var shift = DateTimeOffset.Parse("2026-01-14T17:47:45.765Z") - answered.Created;

        var note = CallLogNote.Write(Shifted(answered, shift), "Spirit");

        Assert.Contains("\nWed Jan 14, 2026, 11:47 AM CST · waited 0:44 · talked 6:14\n", note);
    }

    [Fact]
    public void ACallOverAnHourShowsHoursMinutesAndSecondsOfTalkTime()
    {
        var answered = GoToCallReport.Read(GoToPayloads.Read("report_answered"));
        var connected = answered.Staff.Min(s => s.FirstConnected)!.Value;

        var note = CallLogNote.Write(answered with { Ended = connected + new TimeSpan(1, 2, 5) }, "Spirit");

        Assert.Contains(" · talked 1:02:05\n", note);
    }

    [Fact]
    public void ACallOverADayShowsTotalHoursNotAWrappedOne()
    {
        var answered = GoToCallReport.Read(GoToPayloads.Read("report_answered"));
        var connected = answered.Staff.Min(s => s.FirstConnected)!.Value;

        var note = CallLogNote.Write(answered with { Ended = connected + new TimeSpan(1, 1, 0, 5) }, "Spirit");

        Assert.Contains(" · talked 25:00:05\n", note);
    }

    private static GoToCallReport Shifted(GoToCallReport report, TimeSpan shift)
        => report with
        {
            Created = report.Created + shift,
            Ended = report.Ended + shift,
            Outside = report.Outside! with { FirstConnected = report.Outside.FirstConnected + shift },
            Staff = [.. report.Staff.Select(s => s with { FirstRinging = s.FirstRinging + shift, FirstConnected = s.FirstConnected + shift })],
        };

    private static string Note(string payload, string? brand)
        => CallLogNote.Write(GoToCallReport.Read(GoToPayloads.Read(payload)), brand);
}
