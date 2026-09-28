namespace SpiritAI.Twenty;

/// <summary>
/// A CRM create failed for a reason that is not "this email already exists" — connection refused, a
/// 404, or <see cref="TwentyOptions.BaseUrl"/> left empty.
/// </summary>
public sealed class CrmUnavailableException(Exception? inner = null)
    : Exception("CRM is not set up yet.", inner);
