using AgentCore.Hosting;
using SpiritAI.Auth;
using SpiritAI.Hosting;
using SpiritAI.Knowledge;
using SpiritAI.Lookup;
using SpiritAI.PublicChat;
using SpiritAI.Threads;

var builder = WebApplication.CreateBuilder(args);

builder.AddAgentCoreHost(options => options
    .UseKnowledgeQueryAnalyzers(new IdentifierCodeAnalyzer()));

builder.Services.AddProxyHeaders(builder.Configuration);

builder.Services.AddPublicChat(builder.Configuration);

builder.Services.AddThreadSessions();

builder.Services.AddUnitLookup();

builder.Services.AddSpiritOpenApi();

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

app.UseThreadSessions();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAgentCoreHost();

app.MapPublicChat();

app.MapThreads();

app.MapLookup();

app.UseWidgetFrameAncestors(builder.Configuration);

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.Run();
