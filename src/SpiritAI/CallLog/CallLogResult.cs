namespace SpiritAI.CallLog;

/// <summary>What copying one call did.</summary>
public enum CallLogResult
{
    /// <summary>The note was posted.</summary>
    Copied,

    /// <summary>The conversation already had the call's note.</summary>
    AlreadyCopied,

    /// <summary>The call is not worth a note (<see cref="CallLogNote.IsWorthLogging"/>).</summary>
    Skipped,

    /// <summary>GoTo still had no report after every wait. The next start's catch-up copies it.</summary>
    NotReady,
}
