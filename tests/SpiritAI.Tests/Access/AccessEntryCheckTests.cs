using AgentCore.Application.Configuration.Parsing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SpiritAI.Access;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>The start fails when an access group runs an entry the document does not declare.</summary>
public sealed class AccessEntryCheckTests
{
    [Fact]
    public async Task AMissingEntry_FailsTheStartAndNamesIt()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => StartAsync("main", "dealer", "staff", "manager"));

        Assert.Contains("admin", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryEntryDeclared_Starts()
    {
        await StartAsync("main", "dealer", "staff", "manager", "admin");
    }

    private static async Task StartAsync(params string[] entries)
    {
        var yaml = $$"""
            apiVersion: agentcore/v1
            agents:
              items:
                - { id: spirit, instructions: "answer" }
            entries:
            {{string.Join("\n", entries.Select(entry => $"  {entry}:\n    agent: spirit"))}}
            """;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(ConfigurationLoader.LoadYaml(yaml));
        services.AddAccess();
        await using var provider = services.BuildServiceProvider();

        foreach (var hosted in provider.GetServices<IHostedService>())
        {
            await hosted.StartAsync(TestContext.Current.CancellationToken);
        }
    }
}
