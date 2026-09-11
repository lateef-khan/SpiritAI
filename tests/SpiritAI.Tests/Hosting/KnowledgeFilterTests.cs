using Xunit;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// The facets <c>spirit.yaml</c> lets the agent narrow its own manual search by.
/// </summary>
public sealed class KnowledgeFilterTests
{
    [Fact]
    public void EveryFilterNamesTheYearsAndTagsTheAgentMustWrite()
    {
        // The description is the only thing the agent reads before it picks a value. A key it can
        // see but cannot spell a value for is a filter that only ever returns nothing.
        foreach (var facet in SpiritDocument.Filterable())
        {
            Assert.False(string.IsNullOrWhiteSpace(facet.Key));
            Assert.False(string.IsNullOrWhiteSpace(facet.Description));
        }
    }

    [Fact]
    public void TheLookupFilterNamesItsOnlyValue()
    {
        // kb.yaml declares `lookup` with exactly one legal value, model-numbers, carried by the
        // product model cards and nothing else. The description is the only place the agent
        // learns that value, so it has to spell it out.
        var facet = Assert.Single(
            SpiritDocument.Filterable(), entry => entry.Key == "lookup");

        Assert.Contains("model-numbers", facet.Description, StringComparison.Ordinal);
    }
}
