namespace SpiritAI.Handoffs.Bot;

/// <summary>What <c>business_hours</c> tells the model. Every time is the office's local time.</summary>
/// <param name="Open">Whether the office is open now.</param>
/// <param name="TimeZone">The office's time zone, such as <c>America/Chicago</c>.</param>
/// <param name="Now">The office's day and time now, such as <c>Thursday 14:05</c>.</param>
/// <param name="Today">Today's hours: <c>09:00-17:00</c>, <c>closed</c>, <c>open all day</c>, or <c>no hours set</c>.</param>
/// <param name="NextOpening">
/// When the office next opens, such as <c>Monday 2026-09-28 09:00</c>; <see langword="null"/> when
/// it is open now or keeps no hours.
/// </param>
public sealed record BusinessHoursAnswer(bool Open, string TimeZone, string Now, string Today, string? NextOpening);
