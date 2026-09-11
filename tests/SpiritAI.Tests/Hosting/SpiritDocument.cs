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

    /// <summary>The facets the search tool lets the agent narrow by.</summary>
    internal static IReadOnlyList<FilterableFacet> Filterable()
        => Loaded.Value.Providers?.Knowledge?.Scope?.Filterable
            ?? throw new InvalidOperationException("spirit.yaml declares no scope.filterable block.");

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
        public Providers? Providers { get; set; }
    }

    /// <summary>The <c>providers:</c> block, down to the scope.</summary>
    internal sealed class Providers
    {
        public KnowledgeProvider? Knowledge { get; set; }
    }

    internal sealed class KnowledgeProvider
    {
        public KnowledgeScope? Scope { get; set; }
    }

    internal sealed class KnowledgeScope
    {
        public List<FilterableFacet>? Filterable { get; set; }
    }

    /// <summary>One facet the agent may filter its own search by.</summary>
    internal sealed class FilterableFacet
    {
        public string? Key { get; set; }

        public string? Description { get; set; }
    }
}
