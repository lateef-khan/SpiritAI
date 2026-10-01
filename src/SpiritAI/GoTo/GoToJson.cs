using System.Text.Json;

namespace SpiritAI.GoTo;

/// <summary>Reads GoTo's JSON, where any field can be missing.</summary>
internal static class GoToJson
{
    /// <summary>The element at <paramref name="path"/>, when every step is an object that has it.</summary>
    public static bool TryGet(JsonElement element, out JsonElement found, params string[] path)
    {
        found = element;

        foreach (var name in path)
        {
            if (found.ValueKind != JsonValueKind.Object || !found.TryGetProperty(name, out found))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The string at <paramref name="path"/>, or null.</summary>
    public static string? Text(JsonElement element, params string[] path)
        => TryGet(element, out var found, path) && found.ValueKind == JsonValueKind.String ? found.GetString() : null;

    /// <summary>The items of the array called <paramref name="name"/>; none when it is missing.</summary>
    public static IEnumerable<JsonElement> Items(JsonElement element, string name)
        => TryGet(element, out var found, name) && found.ValueKind == JsonValueKind.Array ? found.EnumerateArray() : [];
}
