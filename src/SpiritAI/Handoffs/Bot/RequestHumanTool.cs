using AgentCore.Application.Ports;

using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;
using SpiritAI.Handoffs.Model;

namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// Hands the chat of the turn under way to staff in Chatwoot.
/// </summary>
public sealed class RequestHumanTool(
    IConversations conversations,
    ChatwootClient chatwoot,
    ChatwootConversationTags tags,
    ListContactFieldsTool contactFields,
    IOptions<CallbackOptions> callback,
    ILogger<RequestHumanTool> logger)
{
    /// <summary>What the model is told on a chat Chatwoot does not hold, such as a signed-in thread.</summary>
    public const string NotAChatwootChat = "A person cannot be reached from this chat.";

    /// <summary>What the model is told when the chat has no phone number to call.</summary>
    public const string NoPhoneNote = "A person will call you back once you leave a phone number.";

    /// <summary>What the model is told when the number it passed cannot be read.</summary>
    public const string BadPhoneNote = " The phone number given is not a valid number; ask for it again.";

    /// <summary>Hands the chat to staff.</summary>
    /// <param name="conversationId">The chat, as AgentCore names it to the binding.</param>
    /// <param name="request">What the model passed.</param>
    /// <param name="cancellationToken">Cancels the handoff.</param>
    /// <returns>One sentence for the model to pass on, with the Chatwoot display id as the code.</returns>
    public async Task<RequestHumanAnswer> AskAsync(string conversationId, HumanRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(conversationId);
        ArgumentNullException.ThrowIfNull(request);

        var record = await conversations.GetAsync(conversationId, cancellationToken).ConfigureAwait(false);

        if (ChatwootIds.Read(record?.Custom) is not { } ids)
        {
            return new RequestHumanAnswer(NotAChatwootChat);
        }

        var code = ids.ConversationId;
        var contactId = (await chatwoot.GetConversationAsync(code, cancellationToken).ConfigureAwait(false)).ContactId;

        if (request.TeamId is > 0 and var teamId)
        {
            await TryAsync("assigning the team", () => chatwoot.AssignTeamAsync(code, teamId, cancellationToken)).ConfigureAwait(false);
        }

        var reach = CheckContactTool.Check(request.Phone, request.Email);

        if (reach.Phone is { } phone)
        {
            await TryAsync(
                    "queueing the call back",
                    async () =>
                    {
                        await tags.SetFieldAsync(code, CallbackQueue.PhoneField, phone, cancellationToken).ConfigureAwait(false);
                        await tags.AddLabelAsync(code, CallbackQueue.Label, cancellationToken).ConfigureAwait(false);
                    })
                .ConfigureAwait(false);
        }

        var emailUpdate = reach.Email is { } email
            ? await SetAsync(contactId, "email", email, cancellationToken).ConfigureAwait(false)
            : null;

        await SetFieldsAsync(contactId, request.ContactFields, cancellationToken).ConfigureAwait(false);

        var note = HandoffNote.Write(
            HandoffNote.Line(request.Phone, reach.Phone is { } e164 ? VisitorPhone.Display(e164) : null),
            HandoffNote.Line(request.Email, reach.Email, emailUpdate),
            request.Summary);

        await chatwoot.PostNoteAsync(code, note, sourceId: null, cancellationToken).ConfigureAwait(false);
        await chatwoot.HandToStaffAsync(code, cancellationToken).ConfigureAwait(false);

        return new RequestHumanAnswer(Promise(request.Phone, reach.Phone, code));
    }

    /// <summary>
    /// Saves one field on the contact through the staff API. A refusal, such as a number another
    /// contact has, is logged without the value; the value stays in the note.
    /// </summary>
    private async Task<ChatwootContactUpdate?> SetAsync(int contactId, string field, string value, CancellationToken cancellationToken)
    {
        ChatwootContactUpdate? update = null;

        await TryAsync(
            "saving " + field,
            async () => update = await chatwoot.UpdateContactFieldAsync(contactId, field, value, cancellationToken).ConfigureAwait(false))
            .ConfigureAwait(false);

        if (update is { Refusal: { } refusal })
        {
            logger.LogWarning("Chatwoot refused {Field} on contact {ContactId}: {Refusal}", field, contactId, refusal);
        }

        return update;
    }

    /// <summary>Saves the fields the chat answered, leaving out any key Chatwoot does not define.</summary>
    private async Task SetFieldsAsync(int contactId, IReadOnlyDictionary<string, string>? values, CancellationToken cancellationToken)
    {
        if (values is null || !values.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
        {
            return;
        }

        await TryAsync(
            "saving the contact fields",
            async () =>
            {
                var defined = (await contactFields.ListAsync(cancellationToken).ConfigureAwait(false)).Fields.Select(f => f.Key).ToHashSet();

                var kept = values
                    .Where(v => defined.Contains(v.Key) && !string.IsNullOrWhiteSpace(v.Value))
                    .ToDictionary(v => v.Key, v => v.Value.Trim());

                if (kept.Count > 0
                    && await chatwoot.UpdateContactAttributesAsync(contactId, kept, cancellationToken).ConfigureAwait(false) is { Refusal: { } refusal })
                {
                    logger.LogWarning("Chatwoot refused the fields on contact {ContactId}: {Refusal}", contactId, refusal);
                }
            }).ConfigureAwait(false);
    }

    /// <summary>A step the handoff goes on without: the note and the status matter more.</summary>
    private async Task TryAsync(string step, Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (HttpRequestException failure)
        {
            logger.LogWarning(failure, "Handoff went on without {Step}.", step);
        }
    }

    private string Promise(string? given, string? e164, int code)
    {
        if (e164 is null)
        {
            var note = string.IsNullOrWhiteSpace(given) ? NoPhoneNote : NoPhoneNote + BadPhoneNote;
            return $"{note} Your code is {code}.";
        }

        var when = callback.Value.Promise is { } text && !string.IsNullOrWhiteSpace(text)
            ? " " + text.Trim().TrimEnd('.')
            : string.Empty;

        return $"We will call you at {VisitorPhone.Display(e164)}{when}. Your code is {code}. If you call us first, give that code.";
    }
}
