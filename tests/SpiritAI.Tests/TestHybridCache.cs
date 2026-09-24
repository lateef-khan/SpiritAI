using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

using ZiggyCreatures.Caching.Fusion;

namespace SpiritAI.Tests;

/// <summary>A fresh in-memory <see cref="HybridCache"/>, built the way the host builds its own.</summary>
internal static class TestHybridCache
{
    public static HybridCache Create()
        => new ServiceCollection()
            .AddFusionCache()
            .AsHybridCache()
            .Services
            .BuildServiceProvider()
            .GetRequiredService<HybridCache>();
}
