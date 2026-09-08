using System.Security.Claims;

namespace SpiritAI.Threads;

/// <summary>
/// Turns a signed-in browser into the opaque key <c>call_principal</c> is filed under.
/// </summary>
public static class CallerPrincipal
{
    /// <summary>What a person's key starts with.</summary>
    public const string UserPrefix = "user:";

    /// <summary>Reads the key one caller's threads are filed under.</summary>
    /// <param name="user">The caller, as the Neon scheme authenticated them.</param>
    /// <returns>The key, or <see langword="null"/> when this caller has no stable identity.</returns>
    public static string? KeyOf(ClaimsPrincipal? user)
    {
        var subject = user?.FindFirstValue(ClaimTypes.NameIdentifier);

        return string.IsNullOrWhiteSpace(subject) ? null : UserPrefix + subject;
    }
}
