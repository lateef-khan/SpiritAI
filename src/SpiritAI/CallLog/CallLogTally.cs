namespace SpiritAI.CallLog;

/// <summary>How many calls the worker copied, found copied already, skipped, found not ready, and failed on.</summary>
internal sealed class CallLogTally
{
    private readonly Dictionary<CallLogResult, int> results = [];

    private int failed;

    /// <summary>Every call counted.</summary>
    public int Processed { get; private set; }

    /// <summary>Counts one call.</summary>
    /// <param name="result">What its copy did, or null when it failed.</param>
    public void Add(CallLogResult? result)
    {
        Processed++;

        if (result is { } done)
        {
            results[done] = results.GetValueOrDefault(done) + 1;
        }
        else
        {
            failed++;
        }
    }

    /// <summary>The counts in the order the worker's log lines name them.</summary>
    /// <returns>Processed, copied, already copied, skipped, not ready, failed.</returns>
    public object[] Counts()
        =>
        [
            Processed,
            results.GetValueOrDefault(CallLogResult.Copied),
            results.GetValueOrDefault(CallLogResult.AlreadyCopied),
            results.GetValueOrDefault(CallLogResult.Skipped),
            results.GetValueOrDefault(CallLogResult.NotReady),
            failed,
        ];
}
