namespace SpiritAI.Neon;

/// <summary>Neon already has a user with this email.</summary>
public sealed class NeonEmailTakenException()
    : Exception("Somebody already has that email in Neon.");
