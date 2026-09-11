namespace SpiritAI.RealTime;

/// <summary>
/// Who sent a signal, stamped on by the hub so a receiver never has to take the sender's word for it.
/// </summary>
/// <param name="Key">The sender's caller key.</param>
/// <param name="Kind">The sender's kind.</param>
public sealed record RealTimeSender(string Key, string Kind);
