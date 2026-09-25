namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo Connect keys Spirit swaps for an access token, and the account it watches. Every value
/// is a secret: set it in user secrets or the host's environment, never in a tracked file.
/// </summary>
public sealed class GoToOptions
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "Goto";

    /// <summary>The GoTo OAuth client's id. The client must have the Personal Access Token grant on.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>The GoTo OAuth client's secret.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Personal Access Token, made by a user with <c>SUPER_USER</c> on the account and the
    /// call-events scopes.
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>The GoTo account whose calls Spirit subscribes to.</summary>
    public string AccountKey { get; set; } = string.Empty;
}
