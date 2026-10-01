using System.Text.Json;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>
/// What GoTo's report of one finished call says: the party outside, the company line, which staff
/// rang and answered, the queue, and whether a voicemail box took a message.
/// </summary>
/// <param name="Id">The <c>conversationSpaceId</c>.</param>
/// <param name="Outbound">Whether staff placed the call. GoTo files a staff-to-staff call as outbound too.</param>
/// <param name="Created">When the call started.</param>
/// <param name="Ended">When it ended.</param>
/// <param name="Outside">The party outside, or null for a staff-to-staff call or a hidden number.</param>
/// <param name="CompanyLine">The company number the call came in on or went out from, in E.164.</param>
/// <param name="Staff">The staff in the call, one per extension.</param>
/// <param name="QueueName">The first call queue it went through, or null.</param>
/// <param name="QueueAbandoned">Whether the caller hung up while a queue held them.</param>
/// <param name="Voicemail">Whether a voicemail box took a message.</param>
public sealed record GoToCallReport(
    string Id,
    bool Outbound,
    DateTimeOffset Created,
    DateTimeOffset Ended,
    GoToOutsideParty? Outside,
    string? CompanyLine,
    IReadOnlyList<GoToReportStaff> Staff,
    string? QueueName,
    bool QueueAbandoned,
    bool Voicemail)
{
    /// <summary>Reads a <c>GET /call-events-report/v1/reports/{id}</c> answer.</summary>
    /// <param name="report">GoTo's answer.</param>
    /// <returns>The call.</returns>
    /// <exception cref="FormatException">The report has no <c>conversationSpaceId</c>, <c>callCreated</c> or <c>callEnded</c>.</exception>
    public static GoToCallReport Read(JsonElement report)
    {
        var id = Text(report, "conversationSpaceId") ?? throw new FormatException("A GoTo call report has no conversationSpaceId.");
        var created = RequiredTime(report, "callCreated", id);
        var ended = RequiredTime(report, "callEnded", id);
        var outbound = Text(report, "direction") == "OUTBOUND";
        var firsts = FirstTimes(report, id);
        GoToOutsideParty? outside = null;
        string? companyLine = null;
        var staff = new Dictionary<string, GoToReportStaff>();

        foreach (var participant in Items(report, "participants"))
        {
            var participantId = Text(participant, "id") ?? string.Empty;

            if (!TryGet(participant, out var type, "type"))
            {
                continue;
            }

            switch (Text(type, "value"))
            {
                case "PHONE_NUMBER" when outside is null:
                    // Inbound the caller is outside; outbound the callee is. A hidden caller's number is "Anonymous".
                    var side = outbound ? "callee" : "caller";

                    if (Text(type, side, "number") is { } number && number.StartsWith('+'))
                    {
                        outside = new GoToOutsideParty(
                            number,
                            outbound ? null : Text(type, side, "name"),
                            FirstAt(firsts, participantId, "CONNECTED"));
                        companyLine = Text(type, "number");
                    }
                    else
                    {
                        companyLine ??= Text(type, "number");
                    }

                    break;

                case "LINE" or "AGENT" when Text(type, "extensionNumber") is { } extension:
                    var person = new GoToReportStaff(
                        Text(type, "name") ?? extension,
                        extension,
                        FirstAt(firsts, participantId, "RINGING"),
                        FirstAt(firsts, participantId, "CONNECTED"));

                    staff[extension] = staff.TryGetValue(extension, out var seen) ? seen.Merge(person) : person;
                    break;
            }
        }

        var systems = Items(report, "interactiveVoiceResponseSystems")
            .Select(system => TryGet(system, out var type, "type") ? type : default)
            .Where(type => type.ValueKind == JsonValueKind.Object)
            .ToList();
        var queues = systems.Where(type => Text(type, "value") == "CALL_QUEUE").ToList();

        return new GoToCallReport(
            id,
            outbound,
            created,
            ended,
            outside,
            companyLine,
            [.. staff.Values],
            queues.Select(queue => Text(queue, "queueName")).FirstOrDefault(name => name is not null),
            queues.Any(queue => Text(queue, "leftQueueReason") == "ABANDON"),
            systems.Any(type => Text(type, "value") == "VOICEMAIL" && Text(type, "voicemailStatus") == "SUCCESS"));
    }

    private static DateTimeOffset? FirstAt(
        Dictionary<(string Participant, string Status), DateTimeOffset> firsts, string participant, string status)
        => firsts.TryGetValue((participant, status), out var at) ? at : null;

    private static DateTimeOffset RequiredTime(JsonElement element, string field, string callId)
        => TryGet(element, out var found, field) && found.ValueKind == JsonValueKind.String && found.TryGetDateTimeOffset(out var at)
            ? at
            : throw new FormatException($"GoTo call report {callId} has no valid {field}.");

    /// <summary>When each participant first showed each status, over every call state.</summary>
    private static Dictionary<(string Participant, string Status), DateTimeOffset> FirstTimes(JsonElement report, string callId)
    {
        var firsts = new Dictionary<(string Participant, string Status), DateTimeOffset>();

        foreach (var state in Items(report, "callStates"))
        {
            var at = RequiredTime(state, "timestamp", callId);

            foreach (var participant in Items(state, "participants"))
            {
                if (Text(participant, "id") is { } participantId
                    && Text(participant, "status", "value") is { } status
                    && (!firsts.TryGetValue((participantId, status), out var seen) || at < seen))
                {
                    firsts[(participantId, status)] = at;
                }
            }
        }

        return firsts;
    }
}
