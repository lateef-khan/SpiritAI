using System.Text.Json;

using static SpiritAI.GoTo.GoToJson;

namespace SpiritAI.GoTo;

/// <summary>Tells a call-report event from a call event on the shared webhook.</summary>
public static class GoToReportEvent
{
    /// <summary>The call a <c>call-events-report</c> event is about.</summary>
    /// <param name="notification">One body GoTo posted.</param>
    /// <returns>The call's <c>conversationSpaceId</c>, or null for any other event.</returns>
    public static string? CallIdOf(JsonElement notification)
        => Text(notification, "source") == "call-events-report" ? Text(notification, "content", "conversationSpaceId") : null;
}
