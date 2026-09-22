namespace SpiritAI.Contacts;

/// <summary>
/// One person.
/// </summary>
public sealed class Contact
{
    /// <summary>The database's own number for the row.</summary>
    public long Id { get; set; }

    /// <summary>What staff see. Filled from SQL or from what the person says.</summary>
    public string? DisplayName { get; set; }

    /// <summary>When the row was made. The database fills it in when left unset.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
