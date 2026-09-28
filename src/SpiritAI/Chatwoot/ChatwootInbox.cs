namespace SpiritAI.Chatwoot;

/// <summary>The Spirit inbox's working hours, as its public route shows them.</summary>
/// <param name="TimeZone">The inbox's IANA time zone, such as <c>America/Chicago</c>.</param>
/// <param name="WorkingHoursEnabled">Whether the inbox keeps working hours. Without them it is always open.</param>
/// <param name="WorkingHours">One entry per day of the week.</param>
public sealed record ChatwootInbox(string TimeZone, bool WorkingHoursEnabled, IReadOnlyList<ChatwootWorkingDay> WorkingHours);
