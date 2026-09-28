namespace SpiritAI.Handoffs.Bot;

/// <summary>What the model passes to <c>request_human</c>.</summary>
/// <param name="Phone">The phone the person gave, or nothing. It is checked again.</param>
/// <param name="Email">The email the person gave, or nothing. It is checked again.</param>
/// <param name="TeamId">The team to hand to, or nothing when no team fits.</param>
/// <param name="ContactFields">The contact fields the chat answered, by key.</param>
/// <param name="Summary">What staff read before they call.</param>
public sealed record HumanRequest(
    string? Phone,
    string? Email,
    int? TeamId,
    IReadOnlyDictionary<string, string>? ContactFields,
    string Summary);
