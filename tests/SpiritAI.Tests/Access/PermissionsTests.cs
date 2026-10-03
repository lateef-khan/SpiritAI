using System.Security.Claims;
using System.Text.Json;

using SpiritAI.Access;

using Xunit;

namespace SpiritAI.Tests.Access;

/// <summary>The permission list of access spec section 4.2.</summary>
public sealed class PermissionsTests
{
    [Fact]
    public void TheKeys_AreTheSpecTable_AgentsStrongestFirst()
    {
        Assert.Equal(
            [
                "chat.agent.admin", "chat.agent.manager", "chat.agent.staff", "chat.agent.dealer", "chat.agent.guest",
                "lookup.units", "lookup.orders", "settings.people", "settings.roles",
            ],
            Permissions.All.Select(info => Permissions.KeyOf(info.Key)));
    }

    [Fact]
    public void OnlyTheFiveChatAgents_AreAgents()
    {
        Assert.Equal(
            [Permission.ChatAgentAdmin, Permission.ChatAgentManager, Permission.ChatAgentStaff, Permission.ChatAgentDealer, Permission.ChatAgentGuest],
            Permissions.All.Where(info => info.Agent).Select(info => info.Key));
    }

    [Fact]
    public void APermission_IsWrittenAsItsKey()
    {
        Assert.Equal("\"lookup.orders\"", JsonSerializer.Serialize(Permission.LookupOrders));
        Assert.Equal(Permission.SettingsRoles, JsonSerializer.Deserialize<Permission>("\"settings.roles\""));
    }

    [Theory]
    [InlineData(new[] { Permission.ChatAgentDealer, Permission.ChatAgentStaff }, Permission.ChatAgentStaff)]
    [InlineData(new[] { Permission.ChatAgentGuest, Permission.ChatAgentAdmin }, Permission.ChatAgentAdmin)]
    [InlineData(new[] { Permission.LookupUnits, Permission.ChatAgentManager }, Permission.ChatAgentManager)]
    public void TheStrongestAgentHeld_Wins(Permission[] held, Permission agent)
    {
        Assert.Equal(agent, Permissions.AgentOf(held));
    }

    [Fact]
    public void NoAgentHeld_IsNoAgent()
    {
        Assert.Null(Permissions.AgentOf([Permission.LookupUnits, Permission.LookupOrders]));
    }

    [Theory]
    [InlineData(Permission.ChatAgentGuest, "main")]
    [InlineData(Permission.ChatAgentDealer, "dealer")]
    [InlineData(Permission.ChatAgentStaff, "staff")]
    [InlineData(Permission.ChatAgentManager, "manager")]
    [InlineData(Permission.ChatAgentAdmin, "admin")]
    public void EachAgent_RunsTheEntryTheSpecNames(Permission agent, string entry)
    {
        Assert.Equal(entry, Permissions.EntryOf(agent));
    }

    [Fact]
    public void AnUnknownKey_DoesNotParse()
    {
        Assert.False(Permissions.TryParse("chat.agent.root", out _));
    }

    [Fact]
    public void OnlyTheSpiritIdentitysClaims_Count()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Permissions.ClaimType, "settings.people")], "neon"));
        user.AddIdentity(new ClaimsIdentity([new Claim(Permissions.ClaimType, "lookup.orders")], Permissions.IdentityType));

        Assert.Equal([Permission.LookupOrders], Permissions.Of(user));
    }
}
