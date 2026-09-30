namespace SpiritAI.Hub;

/// <summary>One app on the Hub's home screen.</summary>
/// <param name="Id">The tile's short name: <c>chat</c>, <c>desk</c>, <c>crm</c>, or <c>settings</c>.</param>
/// <param name="Name">The label shown on the tile.</param>
/// <param name="Url">Where the tile opens.</param>
public sealed record HubTile(string Id, string Name, string Url);
