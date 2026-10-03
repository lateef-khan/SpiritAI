namespace SpiritAI.Neon;

/// <summary>The Neon project Spirit makes and deletes sign-ins in, through Neon's API v2.</summary>
public sealed class NeonOptions
{
    public const string SectionName = "Neon";

    public string ApiUrl { get; set; } = "https://console.neon.tech/api/v2";

    /// <summary>A project-scoped Neon API key. Anyone with it can manage the project.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>The branch Neon Auth runs on; its id starts with <c>br-</c>.</summary>
    public string BranchId { get; set; } = string.Empty;

    /// <summary>
    /// Local only, never prod: also writes or removes the row in this database's own
    /// <c>neon_auth."user"</c>, so a local database follows the real Neon project.
    /// </summary>
    public bool CopyToLocal { get; set; }

    internal bool IsSetUp => ApiKey.Length > 0 && ProjectId.Length > 0 && BranchId.Length > 0;
}
