namespace SpiritAI.CallLog;

/// <summary>Whether GoTo calls are copied into Chatwoot, and how far back a start catches up.</summary>
public sealed class CallLogOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "CallLog";

    /// <summary>Off by default, so a local run never copies real calls into a local Chatwoot.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Where the catch-up starts.
    /// </summary>
    public DateTimeOffset? CatchUpFrom { get; set; }

    /// <summary>How far back a start catches up: the night the app is stopped, and a weekend.</summary>
    public TimeSpan CatchUpWindow { get; set; } = TimeSpan.FromDays(3);

    /// <summary>The waits used when <see cref="ReportWaits"/> is left empty.</summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultReportWaits = [TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60)];

    /// <summary>The waits used when <see cref="ResolveWaits"/> is left empty.</summary>
    public static readonly IReadOnlyList<TimeSpan> DefaultResolveWaits = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)];

    /// <summary>
    /// The waits before each new try at a report GoTo does not have yet.
    /// </summary>
    public TimeSpan[] ReportWaits { get; set; } = [];

    /// <summary>
    /// The waits before each new try at resolving a conversation the copy created: each one listed
    /// is one more try after that wait. Empty means <see cref="DefaultResolveWaits"/>; retries
    /// cannot be turned off.
    /// </summary>
    public TimeSpan[] ResolveWaits { get; set; } = [];

    /// <summary>The waits the copier takes: <see cref="ReportWaits"/>, or <see cref="DefaultReportWaits"/> when none are configured.</summary>
    public IReadOnlyList<TimeSpan> EffectiveReportWaits => ReportWaits.Length > 0 ? ReportWaits : DefaultReportWaits;

    /// <summary>The waits the copier takes: <see cref="ResolveWaits"/>, or <see cref="DefaultResolveWaits"/> when none are configured.</summary>
    public IReadOnlyList<TimeSpan> EffectiveResolveWaits => ResolveWaits.Length > 0 ? ResolveWaits : DefaultResolveWaits;
}
