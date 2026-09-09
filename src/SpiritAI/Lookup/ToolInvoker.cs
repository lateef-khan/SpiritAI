using System.Text.Json;

namespace SpiritAI.Lookup;

/// <summary>
/// Calls one DAB tool.
/// </summary>
/// <remarks>
/// A delegate rather than <c>ToolRegistry</c> itself. The registry has an internal constructor and
/// cannot be built in a test, so this is the seam that lets every reader of these tools be tested
/// without a live database behind Tailscale.
/// </remarks>
/// <param name="toolId">The tool's id, as <c>spirit.yaml</c> aliases it.</param>
/// <param name="arguments">The tool's arguments, by name.</param>
/// <param name="cancellationToken">Cancels the call.</param>
/// <returns>Whatever the tool answered, as JSON.</returns>
public delegate ValueTask<JsonElement> ToolInvoker(
    string toolId,
    IReadOnlyDictionary<string, object?> arguments,
    CancellationToken cancellationToken);
