using AgentCore.AspNetCore.Endpoints;
using AgentCore.Hosting;
using SpiritAI.Access;
using SpiritAI.Auth;
using SpiritAI.CallLog;
using SpiritAI.Caching;
using SpiritAI.Chatwoot;
using SpiritAI.Database;
using SpiritAI.GoTo;
using SpiritAI.Handoffs;
using SpiritAI.Hosting;
using SpiritAI.Hub;
using SpiritAI.Lookup;
using SpiritAI.Neon;
using SpiritAI.PublicChat;
using SpiritAI.Settings;
using SpiritAI.Threads;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSpiritCache(builder.Configuration);

builder.AddSpiritAgentCore();

builder.Services.AddProxyHeaders(builder.Configuration);

builder.Services.AddPublicChat(builder.Configuration);

builder.Services.AddSpiritDatabase(builder.Configuration);

builder.Services.AddAccess();

builder.Services.AddChatwoot(builder.Configuration);

builder.Services.AddHub(builder.Configuration);

builder.Services.AddSettings();

builder.Services.AddNeon(builder.Configuration);

builder.Services.AddGoTo(builder.Configuration);

builder.Services.AddHandoffs();

builder.Services.AddCallLog(builder.Configuration);

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

app.UseAccessBans();

app.UseAuthorization();

app.UseThreadSessions();

app.UseChatwootTurn(AgentCoreExtensions.RouteOf(publicChat.Pattern));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAgentCoreHost(AgentCoreExtensions.ChatResponsesPattern).Responses.SelectEntry<AgentEntrySelector>();

app.MapPublicChat();

app.MapWidgetSettings();

app.MapThreads();

app.MapMe();

app.MapLookup();

app.MapHub();

app.MapSettings();

app.MapGoToWebhook();

app.UseWidgetFrameAncestors(builder.Configuration);

app.UseHubHost(builder.Configuration);

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.MapHubPage(builder.Configuration);

app.Run();
