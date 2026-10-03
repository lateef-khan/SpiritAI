using System.Text.Json.Serialization;

namespace SpiritAI.Access;

/// <summary>Reads and writes a permission by its key only; a number is rejected instead of cast to an undefined value.</summary>
internal sealed class PermissionJsonConverter() : JsonStringEnumConverter<Permission>(allowIntegerValues: false);
