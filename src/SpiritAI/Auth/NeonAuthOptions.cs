namespace SpiritAI.Auth;

/// <summary>
/// Everything the host needs to know about the Neon project that issues its tokens.
/// </summary>
public sealed class NeonAuthOptions
{
    /// <summary>The configuration section these are bound from.</summary>
    public const string SectionName = "Auth:Neon";

    /// <summary>
    /// The Auth Base URL of the Neon project, from the Neon Console under Auth → Configuration.
    /// Shaped like <c>https://ep-xxxx.neonauth.us-east-2.aws.neon.tech/neondb/auth</c>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// The request paths that require a token. Everything else — the health check, the static
    /// chat bundle, the sign-in page — stays open.
    /// </summary>
    public string[] ProtectedPathPrefixes { get; set; } = ["/v1"];

    /// <summary>How far a token's clock may drift from ours before it is refused.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// The shortest interval between two fetches of the key set.
    /// </summary>
    public TimeSpan KeyRefreshCooldown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Where the public keys live. Neon serves them beside the base URL.</summary>
    public Uri JwksUri => new(BaseUriWithTrailingSlash(), ".well-known/jwks.json");

    /// <summary>
    /// The value Neon puts in <c>iss</c> and <c>aud</c>: the origin of the base URL, without the
    /// database and auth path segments.
    /// </summary>
    public string Issuer => new Uri(BaseUrl).GetLeftPart(UriPartial.Authority);

    /// <summary>Whether <see cref="BaseUrl"/> is something the derived values can be built from.</summary>
    /// <param name="problem">What is wrong with it, when the answer is no.</param>
    public bool IsUsable(out string problem)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            problem = $"{SectionName}:BaseUrl is not set. Put the Auth Base URL of your Neon "
                + "project there — Neon Console → Auth → Configuration.";
            return false;
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp))
        {
            problem = $"{SectionName}:BaseUrl is not an absolute http(s) URL: \"{BaseUrl}\".";
            return false;
        }

        problem = string.Empty;
        return true;
    }

    private Uri BaseUriWithTrailingSlash()
        // Without the trailing slash, resolving a relative path against
        // ".../neondb/auth" drops "auth" and asks the wrong host path.
        => new(BaseUrl.EndsWith('/') ? BaseUrl : BaseUrl + "/");
}
