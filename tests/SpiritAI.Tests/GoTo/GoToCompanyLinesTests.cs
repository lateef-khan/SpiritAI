using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>The brand of each company line, from the dial plans GoTo listed on 2026-09-30.</summary>
public sealed class GoToCompanyLinesTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALinesBrandIsItsDialPlanWithoutStart()
    {
        var wire = new ReplayingHandler(["voice_extensions", "voice_phone_numbers"]) { Folder = "GoTo" };
        await using var services = GoToTestServices.Build(wire);
        var lines = services.GetRequiredService<GoToCompanyLines>();

        Assert.Equal("Spirit", await lines.FindBrandAsync("+18002588511", Cancel));
        Assert.Equal("Sole", await lines.FindBrandAsync("+18666976531", Cancel));
        Assert.Equal("Xterra", await lines.FindBrandAsync("+18703364286", Cancel));
        Assert.Null(await lines.FindBrandAsync("+19999999999", Cancel));

        // One read of GoTo answers every line.
        Assert.Equal(2, wire.Requests.Count);
    }
}
