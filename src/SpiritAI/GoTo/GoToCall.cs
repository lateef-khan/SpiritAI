using System.Text.Json;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>
/// What one call event says about a call: which call, which way, the number outside the company,
/// and the staff lines in it.
/// </summary>
/// <param name="Id">The <c>conversationSpaceId</c>, the same on every event of one call.</param>
/// <param name="State"><c>STARTING</c>, <c>ACTIVE</c>, or <c>ENDING</c>.</param>
/// <param name="Outbound">Whether staff placed the call.</param>
/// <param name="OutsideNumber">The caller of an inbound call or the callee of an outbound one, in E.164.</param>
/// <param name="Lines">The staff lines in the call now. A ring group rings several at once.</param>
public sealed record GoToCall(string Id, string State, bool Outbound, string? OutsideNumber, IReadOnlyList<GoToCallLine> Lines)
{
    /// <summary>
    /// Reads a <c>call-state</c> event. A <c>PHONE_NUMBER</c> participant's own <c>number</c> is the
    /// company's line; the outside party is under <c>caller</c> or <c>callee</c>.
    /// </summary>
    /// <param name="callEvent">The event body GoTo posted.</param>
    /// <returns>The call, or null when the event is not about one.</returns>
    public static GoToCall? Read(JsonElement callEvent)
    {
        if (!TryGet(callEvent, out var metadata, "content", "metadata")
            || !TryGet(callEvent, out var state, "content", "state")
            || Text(metadata, "conversationSpaceId") is not { } id)
        {
            return null;
        }

        var outbound = Text(metadata, "direction") == "OUTBOUND";
        string? outside = null;
        var lines = new List<GoToCallLine>();

        if (state.TryGetProperty("participants", out var participants) && participants.ValueKind == JsonValueKind.Array)
        {
            foreach (var participant in participants.EnumerateArray())
            {
                if (!participant.TryGetProperty("type", out var type))
                {
                    continue;
                }

                switch (Text(type, "value"))
                {
                    case "LINE" when Text(type, "lineId") is { } lineId:
                        lines.Add(new GoToCallLine(lineId, Text(type, "extensionNumber") ?? string.Empty, Text(participant, "status", "value") ?? string.Empty));
                        break;

                    case "PHONE_NUMBER":
                        outside ??= Text(type, outbound ? "callee" : "caller", "number");
                        break;
                }
            }
        }

        return new GoToCall(id, Text(state, "type") ?? string.Empty, outbound, outside, lines);
    }
}
