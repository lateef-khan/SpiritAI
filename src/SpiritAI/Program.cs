using AgentCore.Hosting;
using SpiritAI.Auth;
using SpiritAI.Auth.Users;
using SpiritAI.Database;
using SpiritAI.Handoffs;
using SpiritAI.Handoffs.Mail;
using SpiritAI.Handoffs.RealTime;
using SpiritAI.Handoffs.Staff;
using SpiritAI.Handoffs.Visitors;
using SpiritAI.Hosting;
using SpiritAI.Lookup;
using SpiritAI.PublicChat;
using SpiritAI.RealTime;
using SpiritAI.Threads;

var builder = WebApplication.CreateBuilder(args);

builder.AddSpiritAgentCore();

builder.Services.AddProxyHeaders(builder.Configuration);

builder.Services.AddPublicChat(builder.Configuration);

builder.Services.AddThreadSessions();

builder.Services.AddSpiritDatabase(builder.Configuration);

builder.Services.AddNeonUsers();

builder.Services.AddRealTime(builder.Configuration);

builder.Services.AddHandoffs();

builder.Services.AddHandoffMail(builder.Configuration);

builder.Services.AddHandoffRealTime();

builder.Services.AddUnitLookup();

builder.Services.AddSpiritOpenApi();

var publicChat = builder.Configuration.GetSection(PublicChatOptions.SectionName).Get<PublicChatOptions>()
    ?? new PublicChatOptions();

// Configure with "Auth": { "Neon": { "BaseUrl": "https://ep-xxxx.neonauth.<region>.aws.neon.tech/neondb/auth" } }
builder.Services.AddNeonAuth(
    builder.Configuration,
    options =>
    {
        // Every public route sits under one prefix and checks the visitor's key itself. The hub
        // admits visitors with no token, so it does its own check too; see SpiritHub.
        options.OpenPathPrefixes = publicChat.Enabled
            ? [publicChat.PublicPrefix, SpiritHub.Pattern]
            : [SpiritHub.Pattern];
        options.QueryTokenPathPrefixes = [SpiritHub.Pattern];
    });

var app = builder.Build();

app.UseProxyHeaders();

app.UseRateLimiter();

app.UseNeonAuthOnApi();

app.UseThreadSessions();

app.UseVisitorChat(publicChat.Pattern);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapAgentCoreHost();

app.MapPublicChat();

app.MapPublicThreads();

app.MapVisitorHandoffs();

app.MapThreads();

app.MapStaffHandoffs();

app.MapRealTime();

app.MapLookup();

app.UseWidgetFrameAncestors(builder.Configuration);

app.UseStaticFiles();

app.MapFallbackToFile("/chat/{*path:nonfile}", "chat/index.html");

app.Run();
