namespace SpiritAI.Access;

/// <summary>How an <see cref="AccessWriter"/> call ended. Anything but <see cref="Done"/> changed nothing.</summary>
public enum AccessWrite
{
    Done,

    NoSuchPerson,

    NoSuchRole,

    /// <summary>A picked role does not exist.</summary>
    UnknownRoles,

    /// <summary>A role draft with no name.</summary>
    Invalid,

    /// <summary>A ban, a delete or an unlink of the caller's own account.</summary>
    NotYourOwn,

    /// <summary>The caller would take the Admin role from themselves.</summary>
    OwnAdmin,

    /// <summary>The change touches a permission the caller does not hold (ruling R5).</summary>
    BeyondYourAccess,

    /// <summary>An edit or delete of the built-in Admin role.</summary>
    BuiltIn,

    /// <summary>Another role has this name, without regard to case.</summary>
    NameTaken,

    /// <summary>No Admin holder who is not banned would be left.</summary>
    NoAdminLeft,
}
