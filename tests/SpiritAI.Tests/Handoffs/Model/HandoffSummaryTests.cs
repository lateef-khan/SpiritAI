using SpiritAI.Handoffs.Model;

using Xunit;

namespace SpiritAI.Tests.Handoffs.Model;

/// <summary>The summary the AI leaves for staff, section 4 of the phone callback spec.</summary>
public sealed class HandoffSummaryTests
{
    [Fact]
    public void BlankPartsAreEmpty()
    {
        var summary = new HandoffSummary("   ", "", "\t\n", null);

        Assert.Null(summary.Product);
        Assert.Null(summary.Serial);
        Assert.Null(summary.Tried);
        Assert.Null(summary.Wants);
        Assert.True(summary.IsEmpty);
    }

    [Fact]
    public void PartsAreTrimmedAndLeadingZerosKept()
    {
        var summary = new HandoffSummary("  XT485 treadmill ", " 0045210000001234 ", null, null);

        Assert.Equal("XT485 treadmill", summary.Product);
        Assert.Equal("0045210000001234", summary.Serial);
        Assert.False(summary.IsEmpty);
    }

    [Fact]
    public void ALongPartKeepsItsFirst500Characters()
    {
        var summary = new HandoffSummary(null, null, new string('a', 500) + new string('b', 100), null);

        Assert.Equal(new string('a', 500), summary.Tried);
    }
}
