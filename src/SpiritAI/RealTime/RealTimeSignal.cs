using System.Text.Json;

namespace SpiritAI.RealTime;

/// <summary>
/// One socket's word to a group, as <see cref="RealTimeEvents.Signal"/> carries it. A signal is a
/// hint between browsers, such as "typing"; nothing about it is stored.
/// </summary>
/// <param name="Sender">Who sent it, as the hub knows them.</param>
/// <param name="Group">The group it went to.</param>
/// <param name="Name">What kind of signal, named by the feature that uses it.</param>
/// <param name="Payload">Whatever the sender attached, passed through unread.</param>
public sealed record RealTimeSignal(RealTimeSender Sender, string Group, string Name, JsonElement Payload);
