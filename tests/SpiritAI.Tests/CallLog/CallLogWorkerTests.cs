using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SpiritAI.CallLog;
using SpiritAI.Chatwoot;
using SpiritAI.Tests.GoTo;

using Xunit;

namespace SpiritAI.Tests.CallLog;

public sealed class CallLogWorkerTests
{
    private const string AnsweredCall = "84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private const string MenuHangUp = "229bdd49-32b1-3180-8e2e-6ccf051f388d";

    private static readonly Regex GuidShaped = new("[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex PhoneShaped = new(@"\+?\d[\d\s().-]{8,}\d", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACallThatTimesOutInChatwootIsLoggedAndTheNextCallStillRuns()
    {
        var goTo = new ReplayingHandler(["report_answered", "report_menu_hangup"]) { Folder = "GoTo" };
        await using var goToServices = GoToTestServices.Build(goTo);
        var copier = Copier(goToServices, new TimingOutHandler());
        await using var services = new ServiceCollection().AddScoped(_ => copier).BuildServiceProvider();

        var queue = new CallLogQueue();
        var logger = new WatchingLogger(line => line == $"Call log Skipped for call {MenuHangUp}.");
        using var worker = new CallLogWorker(queue, services.GetRequiredService<IServiceScopeFactory>(), logger);

        queue.Add(AnsweredCall);
        queue.Add(MenuHangUp);
        await worker.StartAsync(Cancel);

        await Task.WhenAny(logger.Seen, worker.ExecuteTask!).WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        Assert.True(logger.Seen.IsCompletedSuccessfully);
        Assert.False(worker.ExecuteTask!.IsCompleted);

        await worker.StopAsync(Cancel);
    }

    [Fact]
    public async Task WhenNoCallIsWaitingOneLineGivesTheTotalsAndNothingElseDoes()
    {
        var logger = await RunAsync(3, line => line.StartsWith("Call log queue empty", StringComparison.Ordinal));

        Assert.Equal(
            "Call log queue empty: 3 processed (0 copied, 0 already copied, 3 skipped, 0 not ready, 0 failed).",
            Assert.Single(logger.Lines(LogLevel.Information)));
    }

    [Fact]
    public async Task EveryFiveHundredthCallLogsProgressWithTheCallsStillWaiting()
    {
        var logger = await RunAsync(CallLogWorker.ProgressEvery + 1, line => line.StartsWith("Call log queue empty", StringComparison.Ordinal));

        Assert.Equal(
            [
                $"Call log progress: {CallLogWorker.ProgressEvery} processed (0 copied, 0 already copied, {CallLogWorker.ProgressEvery} skipped, 0 not ready, 0 failed), 1 waiting.",
                $"Call log queue empty: {CallLogWorker.ProgressEvery + 1} processed (0 copied, 0 already copied, {CallLogWorker.ProgressEvery + 1} skipped, 0 not ready, 0 failed).",
            ],
            logger.Lines(LogLevel.Information));
    }

    /// <summary>Runs a worker over menu hang-ups, which GoTo reports and Chatwoot never hears of, until a line it logs satisfies <paramref name="until"/>.</summary>
    private static async Task<WatchingLogger> RunAsync(int calls, Func<string, bool> until)
    {
        var goTo = new ReplayingHandler("report_menu_hangup") { Folder = "GoTo" };
        await using var goToServices = GoToTestServices.Build(goTo);
        var copier = Copier(goToServices, new ReplayingHandler("message_search_none"));
        await using var services = new ServiceCollection().AddScoped(_ => copier).BuildServiceProvider();

        var queue = new CallLogQueue();
        var logger = new WatchingLogger(until);
        using var worker = new CallLogWorker(queue, services.GetRequiredService<IServiceScopeFactory>(), logger);

        for (var call = 0; call < calls; call++)
        {
            queue.Add(MenuHangUp);
        }

        await worker.StartAsync(Cancel);
        await logger.Seen.WaitAsync(TimeSpan.FromSeconds(30), Cancel);
        await worker.StopAsync(Cancel);

        Assert.All(logger.Lines(), AssertNoPersonalData);

        return logger;
    }

    private static void AssertNoPersonalData(string line)
    {
        var withoutIds = GuidShaped.Replace(line, string.Empty);

        Assert.DoesNotMatch(PhoneShaped, withoutIds);
        Assert.DoesNotContain("Test Person", line, StringComparison.Ordinal);
        Assert.DoesNotContain("PROBE CALLER", line, StringComparison.Ordinal);
    }

    private static CallLogCopier Copier(ServiceProvider goToServices, HttpMessageHandler chatwoot)
        => new(
            goToServices.GetRequiredService<SpiritAI.GoTo.GoToClient>(),
            goToServices.GetRequiredService<SpiritAI.GoTo.GoToCompanyLines>(),
            new ChatwootClient(new HttpClient(chatwoot), Options.Create(new ChatwootOptions
            {
                BaseUrl = "http://chatwoot.test/",
                AccountId = 2,
                InboxId = 1,
                BotToken = "bot-token",
                ServiceToken = "service-token",
            })),
            Options.Create(new CallLogOptions { ReportWaits = [TimeSpan.Zero], ResolveWaits = [TimeSpan.Zero] }),
            TimeProvider.System,
            NullLogger<CallLogCopier>.Instance);

    /// <summary>Finds no copied note, then times out on every other call to Chatwoot.</summary>
    private sealed class TimingOutHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/search/messages", StringComparison.Ordinal))
            {
                throw new TaskCanceledException("The request timed out.", new TimeoutException());
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(
                    await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Chatwoot", "Payloads", "message_search_none.json"), cancellationToken),
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json")),
            };
        }
    }

    /// <summary>Keeps every line the worker logs and completes <see cref="Seen"/> when one satisfies the watch.</summary>
    private sealed class WatchingLogger(Func<string, bool> watch) : ILogger<CallLogWorker>
    {
        private readonly object gate = new();

        private readonly List<(LogLevel Level, string Line)> lines = [];

        private readonly TaskCompletionSource seen = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Seen => seen.Task;

        public IReadOnlyList<string> Lines(LogLevel? level = null)
        {
            lock (gate)
            {
                return [.. lines.Where(l => level is null || l.Level == level).Select(l => l.Line)];
            }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var line = formatter(state, exception);

            lock (gate)
            {
                lines.Add((logLevel, line));
            }

            if (watch(line))
            {
                seen.TrySetResult();
            }
        }
    }
}
