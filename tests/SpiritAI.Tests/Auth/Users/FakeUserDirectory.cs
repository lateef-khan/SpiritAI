using SpiritAI.Auth.Users;

namespace SpiritAI.Tests.Auth.Users;

/// <summary>An <see cref="IUserDirectory"/> over a handful of people a test names up front.</summary>
internal sealed class FakeUserDirectory(params AuthUser[] people) : IUserDirectory
{
    /// <inheritdoc />
    public ValueTask<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
        => ValueTask.FromResult(people.FirstOrDefault(
            person => string.Equals(person.Email, email, StringComparison.OrdinalIgnoreCase)));
}
