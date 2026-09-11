using Xunit;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// The facets <c>spirit.yaml</c> lets the agent narrow its own manual search by.
/// </summary>
public sealed class KnowledgeFilterTests
{
    [Fact]
    public void TheModelFilterNamesTheModelSlot()
    {
        // AgentCore folds a filter value through the vocabulary of the state slot with the SAME
        // name, which is what turns "the 2023 LCR" into the stored lcr-2023. A key that drifts off
        // the slot name still boots and still offers the filter -- and then every value the agent
        // writes is dropped as one the collection has never held, so every filtered search falls
        // back to an unfiltered one and nothing says why.
        var facet = Assert.Single(
            SpiritDocument.Filterable(), entry => entry.Key == "model");

        Assert.Contains("model", SpiritDocument.Slots().Keys);
        Assert.NotNull(facet.Description);
    }

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
