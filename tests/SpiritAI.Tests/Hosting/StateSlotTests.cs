
using Xunit;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// The state slots <c>spirit.yaml</c> declares.
/// </summary>
/// <remarks>
/// The <c>model</c> slot is what carries "an LCR built in 2023" across a call. Without it the agent
/// has no place to hold a machine, no list of which years are real, and falls back on the parts
/// database — which records a year for one LCR row in six.
/// </remarks>
public sealed class StateSlotTests
{
    [Fact]
    public void DeclaresAModelSlotWrittenByTheExtractor()
    {
        var slot = SpiritDocument.Slots()["model"];

        Assert.Equal("string", slot.Type);
        Assert.Equal("extractor", slot.Writer);
    }

    [Fact]
    public void TheModelSlotReadsItsVocabularyFromTheKnowledgeBase()
    {
        var slot = SpiritDocument.Slots()["model"];

        Assert.Equal("knowledge", slot.Vocabulary!.From);
        Assert.Equal("exact", slot.Vocabulary.Linker);
        Assert.True(slot.Vocabulary.RefreshSeconds > 0);
    }

    [Fact]
    public void TheExistingSlotsAreUntouched()
    {
        var slots = SpiritDocument.Slots();

        Assert.Contains("code", slots.Keys);
        Assert.Contains("product_line", slots.Keys);
    }
}
