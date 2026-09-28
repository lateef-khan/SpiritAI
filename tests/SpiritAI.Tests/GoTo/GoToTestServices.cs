using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using SpiritAI.GoTo;

namespace SpiritAI.Tests.GoTo;

/// <summary>The host's own GoTo registration, retry included, with GoTo swapped for a replaying wire.</summary>
internal static class GoToTestServices
{
    /// <summary>The settings every GoTo test runs with. The webhook URL is <c>https://spirit.example.test/goto/webhook/probe</c>.</summary>
    public static readonly Dictionary<string, string?> Settings = new()
    {
        ["Goto:AccountKey"] = "1234567890123456789",
        ["Goto:WebhookBaseUrl"] = "https://spirit.example.test",
        ["Goto:WebhookSecret"] = "probe",
        ["Goto:ChannelNickname"] = "spiritprobe",
    };

    public static ServiceProvider Build(ReplayingHandler wire)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(Settings).Build();

        var services = new ServiceCollection().AddGoToHttpClients(configuration);

        services.AddHttpClient(nameof(IGoToNotificationChannelApiClient)).ConfigurePrimaryHttpMessageHandler(() => wire);
        services.AddHttpClient(nameof(IGoToCallEventsApiClient)).ConfigurePrimaryHttpMessageHandler(() => wire);
        services.AddHttpClient(nameof(IGoToDirectoryApiClient)).ConfigurePrimaryHttpMessageHandler(() => wire);
        services.AddSingleton(TestHybridCache.Create());
        services.AddTransient<GoToStaffDirectory>();
        services.AddSingleton<IGoToAuthTokenProvider>(new FixedToken());
        services.AddLogging();

        return services.BuildServiceProvider();
    }

    private sealed class FixedToken : IGoToAuthTokenProvider
    {
        public Task<string> GetBearerTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("fake-goto-access-token");

        public Task InvalidateBearerTokenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
