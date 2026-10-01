using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>The report event GoTo posted to the probe channel in Task 1, and a call event.</summary>
public sealed class GoToReportEventTests
{
    [Fact]
    public void AReportEventNamesItsCall()
        => Assert.Equal("84dcee04-ba4d-33c5-b00b-c0e57599fa69", GoToReportEvent.CallIdOf(GoToPayloads.Read("report_summary_event")));

    [Fact]
    public void ACallEventIsNotAReportEvent()
        => Assert.Null(GoToReportEvent.CallIdOf(GoToPayloads.Read("call_ringing")));
}
