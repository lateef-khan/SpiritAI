using Microsoft.Extensions.Caching.Hybrid;

namespace SpiritAI.GoTo;

/// <summary>
/// The brand behind each company number: the dial plan it rings, without <c>Start</c>. The whole
/// account is read at once and kept for an hour.
/// </summary>
public sealed class GoToCompanyLines(GoToClient goTo, HybridCache cache)
{
    private const string CacheKey = "goto:company-lines";

    private const string DialPlanEnding = " Start";

    private static readonly HybridCacheEntryOptions OneHour = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromHours(1),
    };

    /// <summary>The brand of a company number, such as <c>Spirit</c>.</summary>
    /// <param name="companyLine">The number in E.164.</param>
    /// <param name="cancellationToken">Cancels a read of the account.</param>
    /// <returns>The brand, or null for a number GoTo does not route to a dial plan.</returns>
    public async Task<string?> FindBrandAsync(string companyLine, CancellationToken cancellationToken)
    {
        var lines = await cache.GetOrCreateAsync(CacheKey, ReadAsync, OneHour, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return lines.GetValueOrDefault(companyLine);
    }

    private async ValueTask<Dictionary<string, string>> ReadAsync(CancellationToken cancellationToken)
    {
        var lines = await goTo.ListCompanyLinesAsync(cancellationToken).ConfigureAwait(false);

        return lines.ToDictionary(
            line => line.Key,
            line => line.Value.EndsWith(DialPlanEnding, StringComparison.Ordinal) ? line.Value[..^DialPlanEnding.Length] : line.Value);
    }
}
