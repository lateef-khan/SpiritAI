namespace SpiritAI.Twenty;

/// <summary>
/// A CRM call failed: connection refused, a timeout, an error status, or a reply that is not the
/// fork's. <see cref="NotSetUp"/> makes the one case where <see cref="TwentyOptions.BaseUrl"/> is empty.
/// </summary>
public sealed class CrmUnavailableException : Exception
{
    public CrmUnavailableException(Exception? inner = null)
        : base("CRM did not answer.", inner)
    {
    }

    private CrmUnavailableException(string message)
        : base(message)
    {
    }

    public static CrmUnavailableException NotSetUp() => new("CRM is not set up yet.");
}
