using System.Buffers;

namespace SpiritAI.Handoffs.Visitors;

/// <summary>
/// Turns the random key the widget keeps in <c>localStorage</c> into the opaque key its calls are
/// filed under. Section 4.4 of the handoff spec.
/// </summary>
public static class VisitorPrincipal
{
    /// <summary>The header every public request carries the key in.</summary>
    public const string Header = "X-Spirit-Visitor";

    /// <summary>What a visitor's key starts with, so it can never collide with a signed-in person's.</summary>
    public const string Prefix = "visitor:";

    /// <summary>The longest key accepted.</summary>
    public const int MaxLength = 128;

    private static readonly SearchValues<char> Allowed =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_-");

    /// <summary>The key one visitor's calls are filed under.</summary>
    /// <param name="visitorKey">What the widget sent.</param>
    /// <returns>The key, with the prefix on it.</returns>
    public static string KeyOf(string visitorKey)
    {
        ArgumentNullException.ThrowIfNull(visitorKey);

        return Prefix + visitorKey;
    }

    /// <summary>
    /// Whether a key the browser sent is one this host will file calls under: not empty, at most
    /// <see cref="MaxLength"/> characters, and nothing but letters, digits, <c>_</c>, and <c>-</c>.
    /// </summary>
    /// <param name="visitorKey">What the widget sent.</param>
    /// <returns><see langword="true"/> when the key may be used.</returns>
    public static bool IsWellFormed(string? visitorKey)
        => visitorKey is { Length: > 0 and <= MaxLength }
            && !visitorKey.AsSpan().ContainsAnyExcept(Allowed);
}
