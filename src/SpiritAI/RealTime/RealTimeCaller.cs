namespace SpiritAI.RealTime;

/// <summary>
/// Who is on a socket, as the admission that let them in described them.
/// </summary>
/// <param name="Key">The caller key: the same one their REST requests act under.</param>
/// <param name="Name">A display name, when the caller has one.</param>
/// <param name="Kind">Which kind of caller, the word presence counts them under.</param>
/// <param name="Groups">The groups the socket joins on admission.</param>
/// <param name="MaySignal">Whether the socket may send a signal to a given group.</param>
public sealed record RealTimeCaller(
    string Key,
    string? Name,
    string Kind,
    IReadOnlyList<string> Groups,
    Func<string, bool> MaySignal);
