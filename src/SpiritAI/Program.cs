using AgentCore.Hosting;
using SpiritAI.Auth;
using SpiritAI.Hosting;
using SpiritAI.Knowledge;
using SpiritAI.PublicChat;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentCoreHost(options => options
    .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer()));

builder.Services.AddProxyHeaders(builder.Configuration);

builder.Services.AddPublicChat(builder.Configuration);

var publicChat = builder.Configuration.GetSection(PublicChatOptions.SectionName).Get<PublicChatOptions>()
    ?? new PublicChatOptions();

// Configure with "Auth": { "Neon": { "BaseUrl": "https://ep-xxxx.neonauth.<region>.aws.neon.tech/neondb/auth" } }
builder.Services.AddNeonAuth(
    builder.Configuration,
    options => options.OpenPathPrefixes = publicChat.Enabled ? [publicChat.Pattern] : []);

var app = builder.Build();

app.UseProxyHeaders();

app.UseRateLimiter();

app.UseNeonAuthOnApi();

app.MapAgentCoreHost();

app.MapPublicChat();

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
        context.Response.Headers["Content-Security-Policy"] = $"frame-ancestors {frameAncestors}";
    }

    await next();
});

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.Run();
