namespace SpiritAI.Hosting;

/// <summary>
/// Whether to believe the proxy in front about who is calling.
/// </summary>
public sealed class ProxyHeaderOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "ProxyHeaders";

    /// <summary>Whether the header below is read at all.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The header carrying the caller's address.
    /// </summary>
    public string ClientIpHeader { get; set; } = "Fly-Client-IP";
}
