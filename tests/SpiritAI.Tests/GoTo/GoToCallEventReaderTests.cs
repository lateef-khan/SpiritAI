using System.Text.Json;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SpiritAI.GoTo;

using Xunit;

namespace SpiritAI.Tests.GoTo;

/// <summary>The reader over a real queue, with handlers that record what they were given.</summary>
public sealed class GoToCallEventReaderTests
{
    private const string ReportedCall = "84dcee04-ba4d-33c5-b00b-c0e57599fa69";

    private const string SecondReportedCall = "5c0e1f7a-2d3b-3c4d-8e9f-0a1b2c3d4e5f";

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task AReportEventGoesToTheReportHandlersAndNotToTheCallHandlers()
    {
        var reports = new RecordingReportHandler();
        var calls = new RecordingCallHandler();
        var queue = new GoToCallEventQueue();
        await using var host = new ReaderHost(queue, reports, calls, new CapturingLogger<GoToCallEventReader>());
        var reader = host.Reader;

        await reader.StartAsync(TestContext.Current.CancellationToken);
        queue.Add(GoToPayloads.Read("report_summary_event"));
        await reports.Handled(ReportedCall);
        await reader.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal([ReportedCall], reports.CallIds);
        Assert.Empty(calls.Calls);
    }

    [Fact]
    public async Task AReportHandlerThatTimesOutIsLoggedAndTheNextEventIsStillHandled()
    {
        var reports = new RecordingReportHandler { FailFirstWith = new TaskCanceledException("The request timed out.", new TimeoutException()) };
        var logger = new CapturingLogger<GoToCallEventReader>();
        var queue = new GoToCallEventQueue();
        await using var host = new ReaderHost(queue, reports, new RecordingCallHandler(), logger);
        var reader = host.Reader;

        await reader.StartAsync(TestContext.Current.CancellationToken);
        queue.Add(GoToPayloads.Read("report_summary_event"));
        queue.Add(ReportEvent(SecondReportedCall));
        await reports.Handled(SecondReportedCall);
        await reader.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal([ReportedCall, SecondReportedCall], reports.CallIds);

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains(ReportedCall, warning.Message, StringComparison.Ordinal);
        Assert.IsType<TaskCanceledException>(warning.Exception);
    }

    /// <summary>A reader with the provider that serves its handlers, both disposed together.</summary>
    private sealed class ReaderHost : IAsyncDisposable
    {
        private readonly ServiceProvider services;

        public ReaderHost(GoToCallEventQueue queue, IGoToCallReportHandler reports, IGoToCallHandler calls, ILogger<GoToCallEventReader> logger)
        {
            services = new ServiceCollection()
                .AddSingleton(reports)
                .AddSingleton(calls)
                .BuildServiceProvider();

            Reader = new GoToCallEventReader(queue, services.GetRequiredService<IServiceScopeFactory>(), logger);
        }

        public GoToCallEventReader Reader { get; }

        public async ValueTask DisposeAsync()
        {
            Reader.Dispose();
            await services.DisposeAsync();
        }
    }

    private static JsonElement ReportEvent(string callId)
        => JsonDocument.Parse($$$"""{"source":"call-events-report","type":"REPORT_SUMMARY","content":{"conversationSpaceId":"{{{callId}}}"}}""").RootElement;

    private sealed class RecordingReportHandler : IGoToCallReportHandler
    {
        private readonly Dictionary<string, TaskCompletionSource> handled = [];

        private readonly Lock gate = new();

        private bool failed;

        public Exception? FailFirstWith { get; init; }

        public List<string> CallIds { get; } = [];

        public Task Handled(string callId)
        {
            TaskCompletionSource source;

            lock (gate)
            {
                source = SourceFor(callId);
            }

            return source.Task.WaitAsync(Bound, TestContext.Current.CancellationToken);
        }

        public Task HandleAsync(string conversationSpaceId, CancellationToken cancellationToken)
        {
            lock (gate)
            {
                CallIds.Add(conversationSpaceId);
                SourceFor(conversationSpaceId).TrySetResult();

                if (FailFirstWith is not null && !failed)
                {
                    failed = true;
                    throw FailFirstWith;
                }
            }

            return Task.CompletedTask;
        }

        private TaskCompletionSource SourceFor(string callId)
        {
            if (!handled.TryGetValue(callId, out var source))
            {
                handled[callId] = source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            return source;
        }
    }

    private sealed class RecordingCallHandler : IGoToCallHandler
    {
        public List<GoToCall> Calls { get; } = [];

        public Task HandleAsync(GoToCall call, CancellationToken cancellationToken)
        {
            Calls.Add(call);

            return Task.CompletedTask;
        }
    }
}
