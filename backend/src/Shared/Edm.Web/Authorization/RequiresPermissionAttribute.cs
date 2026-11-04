using Edm.SharedKernel.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace Edm.Web.Authorization;

/// <summary>
/// Declares the single permission an endpoint needs (ADR-0007, Part 20). Works on controllers and minimal APIs.
/// The permission must be in the catalogue, so a typo fails at startup, not at request time.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RequiresPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "edm:permission:";

    public RequiresPermissionAttribute(string permission)
    {
        if (!PermissionCatalogue.IsKnown(permission))
        {
            throw new ArgumentException($"'{permission}' is not in the permission catalogue (Part 20.1).", nameof(permission));
        }

        Permission = permission;
        Policy = PolicyPrefix + permission;
    }

    public string Permission { get; }
}

public static class EndpointPermissionExtensions
{
    /// <summary>Requires the named permission on this endpoint and records it as endpoint metadata.</summary>
    public static TBuilder RequiresPermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.WithMetadata(new RequiresPermissionAttribute(permission));
    }
}
