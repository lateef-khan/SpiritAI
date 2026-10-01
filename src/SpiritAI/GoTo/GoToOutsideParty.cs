namespace SpiritAI.GoTo;

/// <summary>The party outside the company in a call report.</summary>
/// <param name="Number">Their number in E.164.</param>
/// <param name="CallerIdName">The caller-id name of an inbound caller; null outbound.</param>
/// <param name="FirstConnected">When they were first connected, or null when they never were.</param>
public sealed record GoToOutsideParty(string Number, string? CallerIdName, DateTimeOffset? FirstConnected);
