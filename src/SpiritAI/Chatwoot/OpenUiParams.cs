using System.Text.Json;

namespace SpiritAI.Chatwoot;

/// <summary>
/// Each component's argument names in position order, from <c>openui-params.json</c>.
/// </summary>
internal static class OpenUiParams
{
    private const string Resource = "SpiritAI.Chatwoot.openui-params.json";

    private static readonly Dictionary<string, string[]> ByComponent = Load();

    /// <summary>The position of <paramref name="param"/> in <paramref name="component"/>'s arguments, or -1.</summary>
    public static int IndexOf(string component, string param)
        => ByComponent.TryGetValue(component, out var names) ? Array.IndexOf(names, param) : -1;

    private static Dictionary<string, string[]> Load()
    {
        using var stream = typeof(OpenUiParams).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"The embedded resource {Resource} is missing.");

        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)
            ?? throw new InvalidOperationException($"The embedded resource {Resource} is empty.");
    }
}
