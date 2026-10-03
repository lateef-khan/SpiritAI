namespace SpiritAI.Access;

/// <summary>The built-in role that holds every permission. Its id is fixed so nothing looks it up by name.</summary>
public static class AdminRole
{
    public static readonly Guid Id = new("a0000000-0000-4000-8000-000000000001");

    public const string Name = "Admin";
}
