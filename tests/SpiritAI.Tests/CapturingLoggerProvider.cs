using Microsoft.Extensions.Logging;

namespace SpiritAI.Tests;

/// <summary>Hands every category one <see cref="CapturingLogger{T}"/>, for a logger the test cannot name.</summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public CapturingLogger<object> Logger { get; } = new();

    public ILogger CreateLogger(string categoryName) => Logger;

    public void Dispose()
    {
    }
}
