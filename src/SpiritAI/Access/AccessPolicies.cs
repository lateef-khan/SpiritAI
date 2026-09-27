namespace SpiritAI.Access;

/// <summary>The authorization policies an endpoint group asks for, by the lowest rank they let in.</summary>
public static class AccessPolicies
{
    /// <summary>Tech Service, Inside Sales, and every group above them: rank 2 and up.</summary>
    public const string Staff = "staff";

    /// <summary>The managers and Admin: rank 3 and up.</summary>
    public const string Manager = "manager";

    /// <summary>Admin alone: rank 4.</summary>
    public const string Admin = "admin";
}
