using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

using SpiritAI.Chatwoot;

namespace SpiritAI.PublicChat;

/// <summary>Maps the route the widget reads its settings from when it starts.</summary>
public static class WidgetSettingsEndpointRouteBuilderExtensions
{
    /// <summary>Where the settings are read, under <see cref="PublicChatOptions.PublicPrefix"/>.</summary>
    public const string Path = "/widget/settings";

    /// <summary>
    /// Maps <c>GET {PublicPrefix}/widget/settings</c>, or nothing when the public route is disabled. Under the
    /// public prefix, it needs no sign-in and runs under the public rate limit.
    /// </summary>
    /// <param name="endpoints">The route builder to map on.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapWidgetSettings(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var settings = endpoints.ServiceProvider.GetRequiredService<IOptions<PublicChatOptions>>().Value;

        if (settings.Enabled)
        {
            endpoints.MapGet(settings.PublicPrefix + Path, Read)
                .WithName("getWidgetSettings")
                .WithTags("Widget");
        }

        return endpoints;
    }

    /// <summary>The settings, or <c>503</c> when this host has no Chatwoot to send the widget to.</summary>
    private static Results<Ok<WidgetSettings>, ProblemHttpResult> Read(IOptions<ChatwootOptions> options)
    {
        var chatwoot = options.Value;

        return chatwoot.BaseUrl.Length > 0 && chatwoot.InboxIdentifier.Length > 0
            ? TypedResults.Ok(new WidgetSettings(chatwoot.BaseUrl, chatwoot.InboxIdentifier))
            : TypedResults.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Chatwoot is not set up.");
    }
}
