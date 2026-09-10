namespace SpiritAI.Lookup;

/// <summary>
/// Which machine a product name and a year name, and the model number it carries.
/// </summary>
/// <remarks>
/// The knowledge base is the authority on which years a machine exists in: it holds a card set per
/// model year, and the parts database records a year for one LCR row in six. So this answers the
/// year question, and a parts lookup never does.
/// </remarks>
/// <param name="Outcome">
/// <c>model</c> when <see cref="ModelNo"/> is known and the parts database confirms it.
/// <c>needs_year</c> when the product covers several years and none was named, or the named one
/// does not exist. <c>no_record</c> when the year exists but no card states a model number, or the
/// parts database does not carry the one that was stated. <c>unknown_product</c> when no model
/// slug matches the name.
/// </param>
/// <param name="Slug">The model slug, such as <c>lcr-2023</c>.</param>
/// <param name="ModelNo">The six digit model number, or <see langword="null"/>.</param>
/// <param name="Years">The years this product was built, in order and without repeats.</param>
/// <param name="Note">One line the agent may repeat about what happened.</param>
public sealed record ModelAnswer(
    string Outcome,
    string? Slug,
    string? ModelNo,
    IReadOnlyList<int> Years,
    string Note);
