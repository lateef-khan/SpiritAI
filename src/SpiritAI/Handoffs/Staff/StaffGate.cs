using System.Security.Claims;

using SpiritAI.Auth.Users;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// Tells a member of staff from any other caller: anyone with a Neon sign-in is staff. The
/// directory is the list; nothing here has to be kept up to date when the team changes.
/// </summary>
public sealed class StaffGate(IUserDirectory directory)
{
    /// <summary>Finds the caller in the directory, named the way a visitor should see them.</summary>
    /// <param name="user">The caller, as the Neon scheme authenticated them.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns>Their entry, or <see langword="null"/> for anyone without a sign-in.</returns>
    public async ValueTask<HandoffStaffMember?> MemberOfAsync(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var email = user?.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var person = await directory.FindByEmailAsync(email, cancellationToken).ConfigureAwait(false);

        return person is null ? null : new HandoffStaffMember(person.Email, VisitorNameOf(person.Name));
    }

    /// <summary>
    /// "Dana Rivera" becomes "Dana R.": the visitor gets a first name and an initial, not the
    /// whole name off the sign-in. A one-word name is shown as it is.
    /// </summary>
    private static string VisitorNameOf(string fullName)
    {
        var words = fullName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return words.Length switch
        {
            0 => string.Empty,
            1 => words[0],
            _ => $"{words[0]} {words[^1][0]}.",
        };
    }
}
