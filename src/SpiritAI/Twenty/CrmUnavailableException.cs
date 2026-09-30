namespace SpiritAI.Twenty;

/// <summary>
/// A CRM create failed: connection refused, a timeout, a 404, a reply that is not the fork's, or
/// <see cref="TwentyOptions.BaseUrl"/> left empty.
/// </summary>
public sealed class CrmUnavailableException(Exception? inner = null)
    : Exception("CRM is not set up yet.", inner);
