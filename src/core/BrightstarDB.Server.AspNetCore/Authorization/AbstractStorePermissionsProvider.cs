#nullable enable

using System.Security.Claims;

namespace BrightstarDB.Server.AspNetCore.Authorization;

public abstract class AbstractStorePermissionsProvider
{
    public virtual bool HasStorePermission(ClaimsPrincipal? userIdentity, string storeName, StorePermissions permissionRequested)
    {
        return (GetStorePermissions(userIdentity, storeName) & permissionRequested) == permissionRequested;
    }

    public abstract StorePermissions GetStorePermissions(ClaimsPrincipal? currentUser, string storeName);
}
