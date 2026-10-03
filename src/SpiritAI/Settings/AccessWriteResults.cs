using SpiritAI.Access;

namespace SpiritAI.Settings;

/// <summary>The answer and the words for each way an access write is refused.</summary>
internal static class AccessWriteResults
{
    public static IResult Refusal(AccessWrite result) => result switch
    {
        AccessWrite.NoSuchPerson or AccessWrite.NoSuchRole => TypedResults.NotFound(),
        AccessWrite.UnknownRoles => Problem("A role you picked does not exist any more. Reload and try again.", StatusCodes.Status400BadRequest),
        AccessWrite.Invalid => Problem("Give the role a name.", StatusCodes.Status400BadRequest),
        AccessWrite.NotYourOwn => Problem("You cannot do this to your own account.", StatusCodes.Status403Forbidden),
        AccessWrite.OwnAdmin => Problem("You cannot remove your own admin access.", StatusCodes.Status403Forbidden),
        AccessWrite.BeyondYourAccess => Problem("You can only give access you have yourself.", StatusCodes.Status403Forbidden),
        AccessWrite.BuiltIn => Problem("The Admin role cannot be changed.", StatusCodes.Status403Forbidden),
        AccessWrite.NameTaken => Problem("A role with that name already exists.", StatusCodes.Status409Conflict),
        AccessWrite.NoAdminLeft => Problem("This would leave Spirit with no admin.", StatusCodes.Status409Conflict),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "not a refusal."),
    };

    private static IResult Problem(string detail, int status) => TypedResults.Problem(detail, statusCode: status);
}
