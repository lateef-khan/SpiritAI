using System.Text.Json;

namespace SpiritAI.Chatwoot;

/// <summary>One day of an inbox's working hours, in the inbox's time zone.</summary>
/// <param name="Day">The day of the week.</param>
/// <param name="ClosedAllDay">Whether the office is closed all day.</param>
/// <param name="OpenAllDay">Whether the office is open all day.</param>
/// <param name="Opens">When the office opens, on a day that is neither.</param>
/// <param name="Closes">When the office closes, on a day that is neither.</param>
public sealed record ChatwootWorkingDay(DayOfWeek Day, bool ClosedAllDay, bool OpenAllDay, TimeOnly? Opens, TimeOnly? Closes)
{
    internal static ChatwootWorkingDay Read(JsonElement day)
        => new(
            (DayOfWeek)day.GetProperty("day_of_week").GetInt32(),
            day.GetProperty("closed_all_day").GetBoolean(),
            day.GetProperty("open_all_day").GetBoolean(),
            TimeOf(day, "open_hour", "open_minutes"),
            TimeOf(day, "close_hour", "close_minutes"));

    /// <summary>An hour of 24 or more reads as the end of the day.</summary>
    private static TimeOnly? TimeOf(JsonElement day, string hour, string minutes)
    {
        if (day.GetProperty(hour).ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var h = day.GetProperty(hour).GetInt32();
        var m = day.GetProperty(minutes).ValueKind == JsonValueKind.Number ? day.GetProperty(minutes).GetInt32() : 0;

        return h >= 24 ? TimeOnly.MaxValue : new TimeOnly(h, m);
    }
}
