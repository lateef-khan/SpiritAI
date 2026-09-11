using System.Security.Claims;

namespace SpiritAI.Handoffs.Staff;

/// <summary>
/// Tells a member of staff from any other signed-in caller.
/// </summary>
internal static class StaffGate
{
    /// <summary>Finds the caller in the staff list.</summary>
    /// <param name="user">The caller, as the Neon scheme authenticated them.</param>
    /// <param name="options">The list.</param>
    /// <returns>Their entry, or <see langword="null"/> for anyone not listed.</returns>
    public static HandoffStaffMember? MemberOf(ClaimsPrincipal? user, HandoffOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var email = user?.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return options.Staff.FirstOrDefault(
            member => string.Equals(member.Email, email, StringComparison.OrdinalIgnoreCase));
    }
}
