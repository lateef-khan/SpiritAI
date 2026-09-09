namespace SpiritAI.Lookup;

/// <summary>
/// One part, as the agent is allowed to repeat it.
/// </summary>
/// <param name="SpNo">The number to order.</param>
/// <param name="Description">What the part is.</param>
/// <param name="Qty">How many the machine takes.</param>
public sealed record PartLine(string SpNo, string Description, int Qty);

/// <summary>
/// What a parts lookup found, and what it still needs to find more.
/// </summary>
/// <remarks>
/// <para>
/// The shape is the point. A product name covers several model numbers whose parts lists are not
/// the same list, so the answer is either parts for ONE model number or a question about which
/// year. Deciding which of those it is happens here, in code, and not in a prompt: an agent asked
/// to make that call reaches for a serial number instead, and the person cannot read one out over
/// the phone while the machine is making the noise.
/// </para>
/// <para>
/// <see cref="Outcome"/> is what the agent branches on. It is a word rather than a flag because a
/// word survives being read back to a model.
/// </para>
/// </remarks>
/// <param name="Outcome">
/// <c>parts</c> when <see cref="Parts"/> holds rows. <c>needs_year</c> when the product covers
/// several years and the person has named none. <c>no_parts</c> when the model is real and its
/// list is empty. <c>unknown_product</c> when nothing matched the name at all.
/// </param>
/// <param name="ModelNo">The model number the parts belong to, or <see langword="null"/>.</param>
/// <param name="ModelName">That model's name, such as <c>SOLE F63 2016</c>.</param>
/// <param name="Parts">The rows that matched, never more than were asked for.</param>
/// <param name="TotalRows">How many rows matched in all, before the cap.</param>
/// <param name="Years">The years this product was built, when a year is what is missing.</param>
/// <param name="Note">One line the agent may repeat about what happened.</param>
public sealed record PartsAnswer(
    string Outcome,
    string? ModelNo,
    string? ModelName,
    IReadOnlyList<PartLine> Parts,
    int TotalRows,
    IReadOnlyList<int> Years,
    string Note);
