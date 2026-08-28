using AgentCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentCoreHost();

var app = builder.Build();

app.MapAgentCoreHost();

// Configure with, e.g.:
//   "Widget": { "AllowedOrigins": [ "https://www.spiritfitness.com" ] }
var widgetOrigins = builder.Configuration.GetSection("Widget:AllowedOrigins").Get<string[]>();

var frameAncestors = widgetOrigins is { Length: > 0 }
    ? string.Join(' ', widgetOrigins)
    : "'none'";

app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/chat"))
    {
        // `frame-ancestors` and not `X-Frame-Options`: the older header cannot express a list, so a
        // second allowed site would mean choosing which one works.
        context.Response.Headers["Content-Security-Policy"] = $"frame-ancestors {frameAncestors}";
    }

    await next();
});

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.Run();
