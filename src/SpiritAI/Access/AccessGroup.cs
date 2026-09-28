using System.Text.Json.Serialization;

namespace SpiritAI.Access;

/// <summary>
/// The eight access groups of the access sheet. A group picks the agent entry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AccessGroup>))]
public enum AccessGroup
{
    Guest,
    Dealer,
    TechService,
    InsideSales,
    InsideSalesSupervisor,
    TechServiceManager,
    InsideSalesManager,
    Admin,
}
