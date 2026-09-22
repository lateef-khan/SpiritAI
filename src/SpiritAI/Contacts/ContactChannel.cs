namespace SpiritAI.Contacts;

/// <summary>
/// How a conversation started. Stored as lowercase text: <c>chat</c>, <c>phone</c>.
/// </summary>
public enum ContactChannel
{
    /// <summary>One widget tab.</summary>
    Chat,

    /// <summary>One phone call.</summary>
    Phone,
}
