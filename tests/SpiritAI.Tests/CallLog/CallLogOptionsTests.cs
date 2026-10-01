using Microsoft.Extensions.Configuration;

using SpiritAI.CallLog;

using Xunit;

namespace SpiritAI.Tests.CallLog;

public sealed class CallLogOptionsTests
{
    [Fact]
    public void ConfiguredWaitsReplaceTheDefaultsInsteadOfJoiningThem()
    {
        var options = Bind(new() { ["CallLog:ReportWaits:0"] = "00:00:02", ["CallLog:ResolveWaits:0"] = "00:00:03" });

        Assert.Equal([TimeSpan.FromSeconds(2)], options.EffectiveReportWaits);
        Assert.Equal([TimeSpan.FromSeconds(3)], options.EffectiveResolveWaits);
    }

    [Fact]
    public void WithNoWaitsConfiguredTheDefaultsApply()
    {
        var options = Bind([]);

        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)], options.EffectiveReportWaits);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)], options.EffectiveResolveWaits);
    }

    private static CallLogOptions Bind(Dictionary<string, string?> settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings).Build().GetSection(CallLogOptions.SectionName).Get<CallLogOptions>() ?? new CallLogOptions();
}
