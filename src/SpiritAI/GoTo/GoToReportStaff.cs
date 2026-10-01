namespace SpiritAI.GoTo;

/// <summary>One member of staff in a call report.</summary>
/// <param name="Name">The name GoTo shows for the line.</param>
/// <param name="Extension">The extension, such as <c>8625</c>.</param>
/// <param name="FirstRinging">When their phone first rang, or null.</param>
/// <param name="FirstConnected">When they first answered, or null when they never did.</param>
public sealed record GoToReportStaff(string Name, string Extension, DateTimeOffset? FirstRinging, DateTimeOffset? FirstConnected)
{
    /// <summary>
    /// The same person seen again, as a line and as a queue agent, or rung twice: the earliest of
    /// each time.
    /// </summary>
    /// <param name="other">The other sighting.</param>
    /// <returns>One person.</returns>
    public GoToReportStaff Merge(GoToReportStaff other) => this with
    {
        FirstRinging = Earliest(FirstRinging, other.FirstRinging),
        FirstConnected = Earliest(FirstConnected, other.FirstConnected),
    };

    private static DateTimeOffset? Earliest(DateTimeOffset? a, DateTimeOffset? b)
        => a is null ? b : b is null ? a : a < b ? a : b;
}
