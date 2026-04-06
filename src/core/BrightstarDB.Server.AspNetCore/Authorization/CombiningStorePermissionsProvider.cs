#nullable enable
using System.Security.Claims;

namespace BrightstarDB.Server.AspNetCore.Authorization
{
    /// <summary>
    /// A permissions provider that provides the union of permissions from two other providers
    /// </summary>
    public class CombiningStorePermissionsProvider(AbstractStorePermissionsProvider first, AbstractStorePermissionsProvider second) : AbstractStorePermissionsProvider
    {
        public override StorePermissions GetStorePermissions(ClaimsPrincipal? currentUser, string storeName)
        {
            return first.GetStorePermissions(currentUser, storeName) |
                   second.GetStorePermissions(currentUser, storeName);
        }
    }
}
