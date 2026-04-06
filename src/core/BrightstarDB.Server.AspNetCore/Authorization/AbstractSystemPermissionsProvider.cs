#nullable enable

using System.Security.Claims;

namespace BrightstarDB.Server.AspNetCore.Authorization;

public abstract class AbstractSystemPermissionsProvider
{
    public virtual bool HasPermissions(ClaimsPrincipal? user, SystemPermissions requestedPermissions)
    {
        return (GetPermissionsForUser(user) & requestedPermissions) == requestedPermissions;
    }

    public abstract SystemPermissions GetPermissionsForUser(ClaimsPrincipal? user);
}
