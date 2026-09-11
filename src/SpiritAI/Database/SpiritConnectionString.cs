using AgentCore.Application.Secrets;

namespace SpiritAI.Database;

/// <summary>
/// Where the <c>spirit</c> schema's connection string comes from: the same secret AgentCore reads.
/// </summary>
/// <remarks>
/// One database, one credential. AgentCore resolves <see cref="KnownSecrets.PostgresConnectionString"/>
/// through its secret chain; this reads the two places that chain reads in a host, the configuration
/// section first and the plain environment variable second, so the two schemas cannot drift apart.
/// </remarks>
internal static class SpiritConnectionString
{
    /// <summary>The configuration key. The <c>AgentCore__Secrets__…</c> environment form lands on it too.</summary>
    public const string ConfigurationKey = "AgentCore:Secrets:" + KnownSecrets.PostgresConnectionStringName;

    /// <summary>The environment variable read when the configuration key is unset.</summary>
    public const string EnvironmentVariable = KnownSecrets.PostgresConnectionStringVariable;

    /// <summary>Reads the connection string, or nothing when neither place holds one.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The connection string, or <see langword="null"/>.</returns>
    public static string? TryRead(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return Present(configuration[ConfigurationKey])
            ?? Present(Environment.GetEnvironmentVariable(EnvironmentVariable));
    }

    /// <summary>Reads the connection string, and refuses to go on without one.</summary>
    /// <param name="configuration">The host's configuration.</param>
    /// <returns>The connection string.</returns>
    /// <exception cref="InvalidOperationException">Neither place holds one.</exception>
    public static string Read(IConfiguration configuration)
        => TryRead(configuration)
            ?? throw new InvalidOperationException(
                $"No PostgreSQL connection string. Set the configuration key '{ConfigurationKey}' "
                + $"or the environment variable '{EnvironmentVariable}'.");

    /// <summary>Reads an empty setting as an unset one, so the next place is tried.</summary>
    private static string? Present(string? value) => value is { Length: > 0 } ? value : null;
}
