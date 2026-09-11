using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SpiritAI.Database;

/// <summary>
/// Builds a <see cref="SpiritDbContext"/> for <c>dotnet ef</c>, without booting the host.
/// </summary>
public sealed class SpiritDbContextFactory : IDesignTimeDbContextFactory<SpiritDbContext>
{
    /// <summary>What the context is pointed at when the environment names no database.</summary>
    private const string Placeholder = "Host=localhost;Database=design";

    /// <inheritdoc />
    public SpiritDbContext CreateDbContext(string[] args)
    {
        var environment = new ConfigurationBuilder().AddEnvironmentVariables().Build();

        var options = new DbContextOptionsBuilder<SpiritDbContext>();
        options.UseSpiritNpgsql(SpiritConnectionString.TryRead(environment) ?? Placeholder);

        return new SpiritDbContext(options.Options);
    }
}
