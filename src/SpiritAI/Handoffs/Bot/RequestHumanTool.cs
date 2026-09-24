using Microsoft.Extensions.Options;

using SpiritAI.Handoffs.Desk;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// The bot's door into the queue, section 9.4 of the handoff spec: what the <c>RequestHuman</c>
/// binding in <c>spirit.yaml</c> runs.
/// </summary>
public sealed class RequestHumanTool(HandoffDesk desk, IOptions<CallbackOptions> callback)
{
    /// <summary>What the model is told when the chat has no phone number to call.</summary>
    public const string NoPhoneNote = "A person will call you back once you leave a phone number.";

    /// <summary>What the model is told when the number it passed cannot be read.</summary>
    public const string BadPhoneNote = " The phone number given is not a valid number; ask for it again.";

    /// <summary>Asks for a person on the chat of the turn under way.</summary>
    /// <param name="conversationId">The chat, as AgentCore names it to the binding.</param>
    /// <param name="reason">Why, in the person's own words.</param>
    /// <param name="phone">The phone number they gave, as they typed it, when they gave one.</param>
    /// <param name="summary">What the model told staff about the chat.</param>
    /// <param name="cancellationToken">Cancels the ask.</param>
    /// <returns>One sentence for the model to pass on.</returns>
    public async Task<RequestHumanAnswer> AskAsync(
        string conversationId, string reason, string? phone, HandoffSummary summary, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);

        var asked = await desk.AskAsync(conversationId, HandoffAskedBy.Bot, reason, summary, cancellationToken).ConfigureAwait(false);

        if (VisitorPhone.TryRead(phone, out var e164))
        {
            await desk.SetPhoneAsync(conversationId, e164, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            e164 = asked.Row.Phone;
        }

        if (e164 is null)
        {
            return new RequestHumanAnswer(string.IsNullOrWhiteSpace(phone) ? NoPhoneNote : NoPhoneNote + BadPhoneNote);
        }

        return new RequestHumanAnswer(Promise(VisitorPhone.Display(e164), asked.Row.Id));
    }

    private string Promise(string phone, long code)
    {
        var when = callback.Value.Promise is { } text && !string.IsNullOrWhiteSpace(text)
            ? " " + text.Trim().TrimEnd('.')
            : string.Empty;

        return $"We will call you at {phone}{when}. Your code is {code}. If you call us first, give that code.";
    }
}
