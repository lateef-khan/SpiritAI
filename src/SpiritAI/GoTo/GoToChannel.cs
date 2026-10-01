using System.Text.Json;

namespace SpiritAI.GoTo;

/// <summary>A GoTo notification channel, which GoTo posts events to.</summary>
/// <param name="ChannelId">GoTo's id for it, such as <c>Webhook.&lt;guid&gt;</c>. A subscription names it.</param>
/// <param name="Nickname">The name its owner gave it; its URL and its delete go by this and the id.</param>
/// <param name="WebhookUrl">Where GoTo posts its events, or null for a channel that is not a webhook.</param>
/// <param name="LifetimeSeconds">How long GoTo keeps it. A webhook channel gets the longest GoTo allows.</param>
public sealed record GoToChannel(string ChannelId, string Nickname, string? WebhookUrl, long LifetimeSeconds)
{
    /// <summary>Reads one channel as GoTo lists or creates it.</summary>
    /// <param name="channel">One channel of GoTo's answer.</param>
    /// <returns>The channel.</returns>
    public static GoToChannel Read(JsonElement channel)
    {
        var url = channel.TryGetProperty("webhookChannelData", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("webhook", out var webhook)
            && webhook.TryGetProperty("url", out var address)
                ? address.GetString()
                : null;

        var lifetime = channel.TryGetProperty("channelLifetime", out var seconds) && seconds.ValueKind == JsonValueKind.Number
            ? seconds.GetInt64()
            : 0;

        return new GoToChannel(
            channel.GetProperty("channelId").GetString()!,
            channel.GetProperty("channelNickname").GetString()!,
            url,
            lifetime);
    }
}
