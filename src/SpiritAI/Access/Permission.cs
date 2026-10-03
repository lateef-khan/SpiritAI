using System.Text.Json.Serialization;

namespace SpiritAI.Access;

/// <summary>
/// One thing Spirit lets a person do. The JSON name is the key the database, the claims and the
/// web app use; <see cref="Permissions.All"/> is the list with each one's group and label.
/// </summary>
[JsonConverter(typeof(PermissionJsonConverter))]
public enum Permission
{
    [JsonStringEnumMemberName("chat.agent.admin")]
    ChatAgentAdmin,

    [JsonStringEnumMemberName("chat.agent.manager")]
    ChatAgentManager,

    [JsonStringEnumMemberName("chat.agent.staff")]
    ChatAgentStaff,

    [JsonStringEnumMemberName("chat.agent.dealer")]
    ChatAgentDealer,

    [JsonStringEnumMemberName("chat.agent.guest")]
    ChatAgentGuest,

    [JsonStringEnumMemberName("lookup.units")]
    LookupUnits,

    [JsonStringEnumMemberName("lookup.orders")]
    LookupOrders,

    [JsonStringEnumMemberName("settings.people")]
    SettingsPeople,

    [JsonStringEnumMemberName("settings.roles")]
    SettingsRoles,
}
