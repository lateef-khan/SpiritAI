using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SpiritAI.Tests.Hosting;

/// <summary>
/// Reads <c>spirit.yaml</c>, so a test can assert on what the app actually boots on.
/// </summary>
/// <remarks>
/// As much of the document as the assertions need, and no more. AgentCore's own loader wants an
/// API key and a live store before it will hand anything back, and none of these questions need
/// either.
/// </remarks>
internal static class SpiritDocument
{
    private static readonly Lazy<Document> Loaded = new(Read);

    /// <summary>The state slots the document declares, by name.</summary>
    internal static IReadOnlyDictionary<string, Slot> Slots()
        => Loaded.Value.State ?? throw new InvalidOperationException("spirit.yaml declares no state: block.");

    /// <summary>The id of every agent the document declares.</summary>
    internal static IReadOnlyList<string> AgentIds()
        => [.. (Loaded.Value.Agents?.Items ?? []).Select(agent => agent.Id ?? string.Empty)];

    /// <summary>What kind of tool one id is: <c>binding</c>, <c>agent</c> or <c>builtin</c>.</summary>
    /// <param name="id">The tool's id.</param>
    /// <returns>Its <c>kind</c>.</returns>
    internal static string ToolKind(string id)
        => (Loaded.Value.Tools ?? []).FirstOrDefault(tool => tool.Id == id)?.Kind
            ?? throw new InvalidOperationException($"spirit.yaml declares no tool '{id}'.");

    private static Document Read()
    {
        var yaml = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "src", "SpiritAI", "config", "spirit.yaml"));

        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<Document>(yaml);
    }

    /// <summary>Walks up from the test binaries to the directory holding the solution.</summary>
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
    internal sealed class Document
    {
        public Dictionary<string, Slot>? State { get; set; }

        public AgentsBlock? Agents { get; set; }

        public List<Tool>? Tools { get; set; }
    }

    /// <summary>The <c>agents:</c> block.</summary>
    internal sealed class AgentsBlock
    {
        public List<Agent>? Items { get; set; }
    }

    /// <summary>One declared agent.</summary>
    internal sealed class Agent
    {
        public string? Id { get; set; }
    }

    /// <summary>One declared tool.</summary>
    internal sealed class Tool
    {
        public string? Id { get; set; }

        public string? Kind { get; set; }
    }

    /// <summary>One declared state slot.</summary>
    internal sealed class Slot
    {
        public string? Type { get; set; }

        public string? Writer { get; set; }

        public SlotVocabulary? Vocabulary { get; set; }
    }

    /// <summary>The <c>vocabulary:</c> block a slot may carry.</summary>
    internal sealed class SlotVocabulary
    {
        public string? From { get; set; }

        public string? Linker { get; set; }

        public int RefreshSeconds { get; set; }
    }
}
