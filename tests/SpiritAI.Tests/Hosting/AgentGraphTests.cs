using Xunit;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// Which agents the document declares, and which tools are bindings rather than agents.
/// </summary>
/// <remarks>
/// Every <c>kind: agent</c> tool is a second model call inside a turn. The unit desk's job never
/// varied — parse the serial, read the unit — and code already did it for the unit panel. The
/// records desk stays: it picks tables and composes queries, which has no fixed shape.
/// </remarks>
public sealed class AgentGraphTests
{
    [Fact]
    public void DeclaresNoUnitDesk()
        => Assert.DoesNotContain("unit_desk", SpiritDocument.AgentIds());

    [Fact]
    public void StillDeclaresTheRecordsDesk()
        => Assert.Contains("records_desk", SpiritDocument.AgentIds());

    [Fact]
    public void StillDeclaresTheHost()
        => Assert.Contains("spirit", SpiritDocument.AgentIds());

    [Fact]
    public void AskUnitIsABinding()
        => Assert.Equal("binding", SpiritDocument.ToolKind("ask_unit"));

    [Fact]
    public void AskRecordsIsStillAnAgent()
        => Assert.Equal("agent", SpiritDocument.ToolKind("ask_records"));
}
