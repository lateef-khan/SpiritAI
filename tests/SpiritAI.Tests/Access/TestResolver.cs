using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SpiritAI.Access;
using SpiritAI.Database;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>Reads one person's access through the resolver <c>AddAccess</c> registers.</summary>
internal static class TestResolver
{
    /// <param name="open">Opens the database the resolver reads.</param>
    /// <param name="userId">The person.</param>
    /// <param name="log">Where the resolver's log goes, when the test reads it.</param>
    public static async Task<PersonAccess> ResolveAsync(Func<SpiritDbContext> open, Guid userId, ILoggerProvider? log = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            if (log is not null)
            {
                logging.AddProvider(log);
            }
        });
        services.AddAccess();
        services.AddScoped(_ => open());

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAccessResolver>().ResolveAsync(userId, TestContext.Current.CancellationToken);
    }
}
