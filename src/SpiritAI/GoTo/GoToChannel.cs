namespace SpiritAI.GoTo;

/// <summary>A GoTo notification channel, which GoTo posts events to.</summary>
/// <param name="ChannelId">GoTo's id for it, such as <c>Webhook.&lt;guid&gt;</c>. A subscription names it.</param>
/// <param name="Nickname">The name its owner gave it; its URL and its delete go by this and the id.</param>
/// <param name="WebhookUrl">Where GoTo posts its events, or null for a channel that is not a webhook.</param>
/// <param name="LifetimeSeconds">How long GoTo keeps it. A webhook channel gets the longest GoTo allows.</param>
public sealed record GoToChannel(string ChannelId, string Nickname, string? WebhookUrl, long LifetimeSeconds);
