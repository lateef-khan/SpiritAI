using System.Text.Json;

namespace SpiritAI.Tests.GoTo;

/// <summary>Reads a saved GoTo answer from <c>GoTo/Payloads</c>.</summary>
internal static class GoToPayloads
{
    public static JsonElement Read(string name)
        => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "GoTo", "Payloads", name + ".json"))).RootElement;
}
