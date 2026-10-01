using System.Globalization;

using PhoneNumbers;

using SpiritAI.GoTo;

namespace SpiritAI.CallLog;

/// <summary>The private note one call gets in Chatwoot.</summary>
public static class CallLogNote
{
    /// <summary>The company's time zone, which every note's time is shown in.</summary>
    public static readonly TimeZoneInfo Central = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    private static readonly PhoneNumberUtil Phones = PhoneNumberUtil.GetInstance();

    /// <summary>
    /// Whether a call gets a note.
    /// </summary>
    /// <param name="report">The call.</param>
    /// <returns>Whether to log it.</returns>
    public static bool IsWorthLogging(GoToCallReport report)
        => report.Outside is not null && report.Staff.Any(s => s.FirstRinging is not null || s.FirstConnected is not null);

    /// <summary>The <c>source_id</c> a call's note carries.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    /// <returns>The source id.</returns>
    public static string SourceId(string callId) => $"goto:{callId}";

    /// <summary>The note's last line, which names the call in GoTo.</summary>
    /// <param name="callId">The call's <c>conversationSpaceId</c>.</param>
    /// <returns>The line.</returns>
    public static string Marker(string callId) => $"GoTo call {callId}";

    /// <summary>A number as staff read it, such as <c>+1 201-555-0100</c>.</summary>
    /// <param name="e164">The number in E.164.</param>
    /// <returns>The number, or <paramref name="e164"/> as it is when it cannot be read.</returns>
    public static string Phone(string e164)
    {
        try
        {
            return Phones.Format(Phones.Parse(e164, "US"), PhoneNumberFormat.INTERNATIONAL);
        }
        catch (NumberParseException)
        {
            return e164;
        }
    }

    /// <summary>Writes the note.</summary>
    /// <param name="report">The call.</param>
    /// <param name="brand">The brand of its company line, or null when GoTo does not say.</param>
    /// <returns>The note, one fact per line.</returns>
    public static string Write(GoToCallReport report, string? brand)
    {
        List<string> lines = [Header(report), LineOf(report, brand)];

        if (!report.Outbound && report.Outside is { } caller)
        {
            lines.Add(CallerId(caller));
        }

        lines.Add(Times(report));
        lines.Add(Marker(report.Id));

        return string.Join('\n', lines);
    }

    private static string Header(GoToCallReport report) => report.Outbound ? OutboundHeader(report) : InboundHeader(report);

    private static string OutboundHeader(GoToCallReport report)
    {
        var withATime = report.Staff
            .Where(s => s.FirstRinging is not null || s.FirstConnected is not null)
            .OrderBy(s => s.FirstRinging ?? s.FirstConnected);

        // With no times at all, the first one GoTo lists is the best guess; none listed, no one is named.
        var caller = withATime.FirstOrDefault() ?? report.Staff.FirstOrDefault();
        var outcome = report.Outside?.FirstConnected is null ? "No answer" : "Answered";

        return caller is null ? $"📞 Outbound call · {outcome}" : $"📞 Outbound call by {Who(caller)} · {outcome}";
    }

    private static string InboundHeader(GoToCallReport report)
    {
        var answered = report.Staff.Where(s => s.FirstConnected is not null).OrderBy(s => s.FirstConnected).ToList();

        if (answered.Count > 0)
        {
            return $"📞 Inbound call · Answered by {string.Join(", then ", answered.Select(Who))}{(report.Voicemail ? " · then voicemail" : string.Empty)}";
        }

        var rang = string.Join(", ", report.Staff.Where(s => s.FirstRinging is not null).OrderBy(s => s.FirstRinging).Select(Who));
        var outcome = report.Voicemail ? "Voicemail" : report.QueueAbandoned ? "Caller hung up in queue" : "Missed";

        return rang.Length > 0 ? $"📞 Inbound call · {outcome} · rang {rang}" : $"📞 Inbound call · {outcome}";
    }

    private static string LineOf(GoToCallReport report, string? brand)
    {
        var line = string.Join(' ', new[] { brand, report.CompanyLine is { } number ? Phone(number) : null }.OfType<string>());

        return report.QueueName is { } queue ? $"Line: {line} → {queue}" : $"Line: {line}";
    }

    private static string CallerId(GoToOutsideParty caller)
    {
        // GoTo pads caller-id names with runs of spaces, such as "GRAND PR     TX".
        var name = string.Join(' ', (caller.CallerIdName ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return name.Length > 0 ? $"Caller ID: {name}, {Phone(caller.Number)}" : $"Caller ID: {Phone(caller.Number)}";
    }

    private static string Times(GoToCallReport report)
    {
        var started = TimeZoneInfo.ConvertTime(report.Created, Central);
        var when = $"{started.ToString("ddd MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture)} {(Central.IsDaylightSavingTime(started) ? "CDT" : "CST")}";

        var connected = report.Outbound
            ? report.Outside?.FirstConnected
            : report.Staff.Select(s => s.FirstConnected).Where(t => t is not null).Min();

        if (connected is not { } at)
        {
            return when;
        }

        var talked = $"talked {Duration(report.Ended - at)}";

        return report.Outbound ? $"{when} · {talked}" : $"{when} · waited {Duration(at - report.Created)} · {talked}";
    }

    private static string Who(GoToReportStaff staff) => $"{staff.Name} ({staff.Extension})";

    private static string Duration(TimeSpan span)
    {
        var whole = TimeSpan.FromSeconds(Math.Max(0, Math.Floor(span.TotalSeconds)));

        return whole.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(long)whole.TotalHours}:{whole:mm\\:ss}")
            : whole.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }
}
