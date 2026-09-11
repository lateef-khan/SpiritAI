namespace SpiritAI.Handoffs.Bot;

/// <summary>
/// What <c>request_human</c> tells the model. A record, so it serialises as an object the model
/// can read; a tuple would reach it as <c>{}</c>.
/// </summary>
/// <param name="Ok">Whether a person was asked for.</param>
/// <param name="Position">Where the chat stands in the line, one for the front. Absent when nobody was asked.</param>
/// <param name="StaffOnline">How many members of staff are on a socket right now.</param>
/// <param name="Note">What to tell the person, in one sentence.</param>
public sealed record RequestHumanAnswer(bool Ok, int? Position, int StaffOnline, string Note);
