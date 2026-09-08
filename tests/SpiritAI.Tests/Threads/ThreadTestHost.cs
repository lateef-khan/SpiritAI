using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Auth;
using SpiritAI.Tests.Auth;

using Xunit;

namespace SpiritAI.Tests.Threads;

/// <summary>
/// The test server both thread suites run against: a real Neon token check, and nothing else.
/// </summary>
internal static class ThreadTestHost
{
    /// <summary>Starts a test server with the Neon token check in front of it.</summary>
    /// <param name="kit">Mints the tokens and the validator that accepts them.</param>
    /// <param name="services">What the suite under test needs registering.</param>
    /// <param name="pipeline">The suite's own middleware and routes.</param>
    /// <param name="auth">Applied to the bound options, for a suite that opens a route.</param>
    /// <returns>The started host.</returns>
    public static Task<IHost> StartAsync(
        NeonAuthTestKit kit,
        Action<IServiceCollection> services,
        Action<IApplicationBuilder> pipeline,
        Action<NeonAuthOptions>? auth = null)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(pipeline);

        return new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(collection =>
                {
                    collection.AddNeonAuth(
                        new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                [$"{NeonAuthOptions.SectionName}:BaseUrl"] = NeonAuthTestKit.BaseUrl,
                            })
                            .Build(),
                        auth);

                    collection.AddSingleton(kit.Validator());
                    collection.AddRouting();

                    services(collection);
                })
                .Configure(pipeline))
            .StartAsync(TestContext.Current.CancellationToken);
    }
}
