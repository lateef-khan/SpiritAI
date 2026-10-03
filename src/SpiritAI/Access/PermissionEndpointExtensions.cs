namespace SpiritAI.Access;

/// <summary>How a route asks for a permission.</summary>
public static class PermissionEndpointExtensions
{
    /// <summary>Lets in only a caller who holds <paramref name="permission"/>.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, Permission permission)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(Permissions.PolicyOf(permission));

    /// <summary>Lets in a caller who holds any one of <paramref name="permissions"/>.</summary>
    public static TBuilder RequireAnyPermission<TBuilder>(this TBuilder builder, params Permission[] permissions)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy =>
            policy.RequireAssertion(context => Permissions.Of(context.User).Overlaps(permissions)));
}
