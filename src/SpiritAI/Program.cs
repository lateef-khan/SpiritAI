using AgentCore.Hosting;
using SpiritAI.Auth;
using SpiritAI.Caching;
using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.Handoffs;
using SpiritAI.Hosting;
using SpiritAI.Lookup;
using SpiritAI.PublicChat;
using SpiritAI.Threads;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpiritCache(builder.Configuration);

builder.AddSpiritAgentCore();

builder.Services.AddProxyHeaders(builder.Configuration);

builder.Services.AddPublicChat(builder.Configuration);

builder.Services.AddThreadSessions();

builder.Services.AddSpiritDatabase(builder.Configuration);

builder.Services.AddChatwoot(builder.Configuration);

builder.Services.AddHandoffs();

builder.Services.AddUnitLookup();

builder.Services.AddSpiritOpenApi();

var publicChat = builder.Configuration.GetSection(PublicChatOptions.SectionName).Get<PublicChatOptions>()
    ?? new PublicChatOptions();

// Configure with "Auth": { "Neon": { "BaseUrl": "https://ep-xxxx.neonauth.<region>.aws.neon.tech/neondb/auth" } }
builder.Services.AddNeonAuth(
    builder.Configuration,
    options =>
    {
        // Every public route sits under one prefix and checks the visitor's key itself.
        options.OpenPathPrefixes = publicChat.Enabled ? [publicChat.PublicPrefix] : [];
    });

var app = builder.Build();

app.UseProxyHeaders();

app.UseRateLimiter();

app.UseNeonAuthOnApi();

app.UseThreadSessions();

app.UseChatwootTurn(AgentCoreExtensions.RouteOf(publicChat.Pattern));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAgentCoreHost();

app.MapPublicChat();

app.MapWidgetSettings();

app.MapThreads();

app.MapLookup();

app.UseWidgetFrameAncestors(builder.Configuration);

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.Run();
