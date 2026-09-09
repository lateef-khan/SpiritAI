using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
        var slot = Slots()["model"];

        Assert.Equal("string", slot.Type);
        Assert.Equal("extractor", slot.Writer);
    }

    [Fact]
    public void TheModelSlotReadsItsVocabularyFromTheKnowledgeBase()
    {
        var slot = Slots()["model"];

        Assert.Equal("knowledge", slot.Vocabulary!.From);
        Assert.Equal("exact", slot.Vocabulary.Linker);
        Assert.True(slot.Vocabulary.RefreshSeconds > 0);
    }

    [Fact]
    public void TheExistingSlotsAreUntouched()
    {
        var slots = Slots();

        Assert.Contains("code", slots.Keys);
        Assert.Contains("product_line", slots.Keys);
    }

    /// <summary>Reads the <c>state:</c> mapping out of the document the app boots on.</summary>
    /// <returns>Every declared slot, by name.</returns>
    private static IReadOnlyDictionary<string, Slot> Slots()
    {
        var yaml = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "SpiritAI", "config", "spirit.yaml"));

        var document = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<Document>(yaml);

        return document.State
            ?? throw new InvalidOperationException("spirit.yaml declares no state: block.");
    }

    /// <summary>Walks up from the test binaries to the directory holding the solution.</summary>
    /// <returns>The repository root.</returns>
    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SpiritAI.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("SpiritAI.slnx is above no parent of the test binaries.");
    }

    /// <summary>As much of the document as these assertions read.</summary>
    private sealed class Document
    {
        public Dictionary<string, Slot>? State { get; set; }
    }

    /// <summary>One declared state slot.</summary>
    private sealed class Slot
    {
        public string? Type { get; set; }

        public string? Writer { get; set; }

        public SlotVocabulary? Vocabulary { get; set; }
    }

    /// <summary>The <c>vocabulary:</c> block a slot may carry.</summary>
    private sealed class SlotVocabulary
    {
        public string? From { get; set; }

        public string? Linker { get; set; }

        public int RefreshSeconds { get; set; }
    }
}
