namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// What <c>request_human</c> tells the model: one sentence to pass on.
/// </summary>
/// <param name="Note">What to tell the person, in one sentence.</param>
public sealed record RequestHumanAnswer(string Note);
