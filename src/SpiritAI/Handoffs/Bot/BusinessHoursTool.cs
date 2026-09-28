using System.Globalization;

using Microsoft.Extensions.Caching.Hybrid;

using SpiritAI.Chatwoot;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Whether the office is open, read from the Spirit inbox's working hours in Chatwoot. The hours
/// are kept for <see cref="Lifetime"/>; whether it is open is worked out on every call.
/// </summary>
public sealed class BusinessHoursTool(ChatwootClient chatwoot, HybridCache cache, TimeProvider clock)
{
    /// <summary>How long the inbox's hours are kept.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private const string Key = "spirit:chatwoot:inbox";

    private static readonly HybridCacheEntryOptions Keep = new() { Expiration = Lifetime, LocalCacheExpiration = Lifetime };

    /// <summary>Reads the hours and says whether the office is open now.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>Open or not, today's hours, and the next opening.</returns>
    public async Task<BusinessHoursAnswer> ReadAsync(CancellationToken cancellationToken)
    {
        var inbox = await cache
            .GetOrCreateAsync(Key, chatwoot, static (client, token) => new ValueTask<ChatwootInbox>(client.GetInboxAsync(token)), Keep, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return At(inbox, clock.GetUtcNow());
    }

    /// <summary>Whether the office is open at a moment, and when it next opens.</summary>
    /// <param name="inbox">The inbox's hours.</param>
    /// <param name="now">The moment.</param>
    /// <returns>The answer, in the office's local time.</returns>
    public static BusinessHoursAnswer At(ChatwootInbox inbox, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(inbox);

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(inbox.TimeZone, out var found) ? found : TimeZoneInfo.Utc;
        var local = TimeZoneInfo.ConvertTime(now, zone);
        var time = TimeOnly.FromDateTime(local.DateTime);
        var nowText = local.ToString("dddd HH:mm", CultureInfo.InvariantCulture);

        if (!inbox.WorkingHoursEnabled)
        {
            return new BusinessHoursAnswer(true, zone.Id, nowText, "no hours set", NextOpening: null);
        }

        var today = DayOf(inbox, local.DayOfWeek);
        var open = today is { ClosedAllDay: false }
            && (today.OpenAllDay || (today.Opens <= time && time < today.Closes));

        return new BusinessHoursAnswer(
            open,
            zone.Id,
            nowText,
            HoursOf(today),
            open ? null : NextOpening(inbox, DateOnly.FromDateTime(local.DateTime), time));
    }

    /// <summary>The first opening after <paramref name="time"/> on <paramref name="date"/>, looking a week ahead.</summary>
    private static string? NextOpening(ChatwootInbox inbox, DateOnly date, TimeOnly time)
    {
        for (var ahead = 0; ahead <= 7; ahead++)
        {
            var day = date.AddDays(ahead);

            if (DayOf(inbox, day.DayOfWeek) is not { ClosedAllDay: false } hours
                || (hours.OpenAllDay ? TimeOnly.MinValue : hours.Opens) is not { } opens
                || (ahead == 0 && opens <= time))
            {
                continue;
            }

            return day.ToDateTime(opens).ToString("dddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static ChatwootWorkingDay? DayOf(ChatwootInbox inbox, DayOfWeek day)
        => inbox.WorkingHours.FirstOrDefault(d => d.Day == day);

    private static string HoursOf(ChatwootWorkingDay? day)
        => day switch
        {
            null or { ClosedAllDay: true } => "closed",
            { OpenAllDay: true } => "open all day",
            { Opens: { } opens, Closes: { } closes } => string.Create(CultureInfo.InvariantCulture, $"{opens:HH:mm}-{closes:HH:mm}"),
            _ => "closed",
        };
}
