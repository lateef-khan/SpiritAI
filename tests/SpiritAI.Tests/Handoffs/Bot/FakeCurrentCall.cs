using SpiritAI.Handoffs.Bot;

namespace SpiritAI.Tests.Handoffs.Bot;

/// <summary>An <see cref="ICurrentCall"/> a test sets by hand.</summary>
internal sealed class FakeCurrentCall : ICurrentCall
{
    public string? CallId { get; set; }
}
