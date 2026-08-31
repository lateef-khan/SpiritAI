using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SpiritAI.Auth;

/// <summary>Names for the scheme, so nothing has to spell the string twice.</summary>
public static class NeonAuthenticationDefaults
{
    /// <summary>The scheme these tokens authenticate under.</summary>
    public const string Scheme = "NeonBearer";
}

/// <summary>
/// Reads <c>Authorization: Bearer</c> and hands the token to <see cref="NeonTokenValidator"/>.
/// </summary>
public sealed class NeonAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    NeonTokenValidator validator)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string BearerPrefix = "Bearer ";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? header = Request.Headers.Authorization;

        if (string.IsNullOrEmpty(header))
        {
            // NoResult, not Fail: a request with no credentials has not failed to authenticate,
            // it simply has not tried. Endpoints that allow anonymous callers depend on this.
            return AuthenticateResult.NoResult();
        }

        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.Fail("The Authorization header is not a bearer token.");
        }

        var token = header[BearerPrefix.Length..].Trim();
        
        if (token.Length == 0)
        {
            return AuthenticateResult.Fail("The bearer token is empty.");
        }

        var result = await validator.ValidateAsync(token, Context.RequestAborted).ConfigureAwait(false);

        if (result.Principal is null)
        {
            if (Logger.IsEnabled(LogLevel.Information))
            {
                // Logged here and nowhere else. The caller gets a bare 401: telling them which check
                // failed is telling an attacker which part of the token to fix next.
                Logger.LogInformation("Rejected a bearer token: {Reason}", result.Failure);
            }

            return AuthenticateResult.Fail(result.Failure ?? "The token is not valid.");
        }

        return AuthenticateResult.Success(new AuthenticationTicket(result.Principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Task.CompletedTask;
    }
}
