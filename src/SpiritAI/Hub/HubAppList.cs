namespace SpiritAI.Hub;

/// <summary>The tiles a caller's Hub shows, in the order they are drawn.</summary>
/// <param name="Tiles">One entry per app the caller may open.</param>
public sealed record HubAppList(IReadOnlyList<HubTile> Tiles);
