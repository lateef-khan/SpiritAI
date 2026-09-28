namespace SpiritAI.Handoffs.Model;

/// <summary>
/// How a conversation waits for a call back in Chatwoot.
/// </summary>
public static class CallbackQueue
{
    /// <summary>The label on a conversation that waits for a call back.</summary>
    public const string Label = "callback";

    /// <summary>The conversation's custom field that holds the number to call, in E.164 form.</summary>
    public const string PhoneField = "callback_phone";
}
