namespace SpiritAI.Hub;

/// <summary>The apps a Person can have a Linked user in, as <c>spirit.linked_user.app</c> spells them.</summary>
public static class HubApps
{
    /// <summary>Chatwoot, the staff inbox.</summary>
    public const string Desk = "desk";

    /// <summary>Twenty, the customer records.</summary>
    public const string Crm = "crm";

    /// <summary>Every app, in tile order.</summary>
    public static IReadOnlyList<string> All { get; } = [Desk, Crm];
}
