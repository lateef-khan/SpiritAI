namespace SpiritAI.Hub;

/// <summary>Where the Hub is served and where its apps live.</summary>
public sealed class HubOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Hub";

    /// <summary>
    /// The host name the Hub answers on, such as <c>hub.spiritfitnessapps.com</c>. Empty serves it
    /// on every host, which is what local runs on <c>localhost</c> want.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>Desk's address, such as <c>https://desk.spiritfitnessapps.com</c>.</summary>
    public string DeskUrl { get; set; } = string.Empty;

    /// <summary>CRM's address, such as <c>https://crm.spiritfitnessapps.com</c>.</summary>
    public string CrmUrl { get; set; } = string.Empty;
}
