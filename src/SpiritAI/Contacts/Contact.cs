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

    /// <summary>The person's contact id in Chatwoot. Null until a chat of theirs is first copied.</summary>
    public int? ChatwootContactId { get; set; }

    /// <summary>
    /// The key Chatwoot made for the person in the Spirit inbox, which every conversation of theirs
    /// is opened under. Null until a chat of theirs is first copied.
    /// </summary>
    public string? ChatwootSourceId { get; set; }
}
