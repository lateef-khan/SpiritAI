using System.Diagnostics.CodeAnalysis;

namespace SpiritAI.GoTo;

/// <summary>
/// The GoTo Connect keys Spirit swaps for an access token, the account it watches, and where GoTo
/// posts call events.
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
    /// The Personal Access Token.
    /// </summary>
    public string PersonalAccessToken { get; set; } = string.Empty;

    /// <summary>The GoTo account whose calls Spirit subscribes to.</summary>
    public string AccountKey { get; set; } = string.Empty;

    /// <summary>Spirit's public https address, such as <c>https://chat.spiritfitnessapps.com</c>.</summary>
    public string WebhookBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// A long random value in the webhook path.
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Spirit's name for its GoTo channel.
    /// </summary>
    public string ChannelNickname { get; set; } = string.Empty;

    /// <summary>
    /// Builds <c>{WebhookBaseUrl}/goto/webhook/{WebhookSecret}</c>, the URL GoTo posts events to.
    /// </summary>
    /// <param name="webhookUrl">The URL, when the three webhook keys are all set.</param>
    /// <returns>False when a webhook key is missing or the base URL is not absolute.</returns>
    public bool TryGetWebhookUrl([NotNullWhen(true)] out Uri? webhookUrl)
    {
        webhookUrl = null;

        if (string.IsNullOrWhiteSpace(WebhookSecret)
            || string.IsNullOrWhiteSpace(ChannelNickname)
            || !Uri.TryCreate(WebhookBaseUrl.TrimEnd('/'), UriKind.Absolute, out var baseUrl))
        {
            return false;
        }

        webhookUrl = new Uri($"{baseUrl.AbsoluteUri.TrimEnd('/')}/goto/webhook/{Uri.EscapeDataString(WebhookSecret)}");

        return true;
    }
}
