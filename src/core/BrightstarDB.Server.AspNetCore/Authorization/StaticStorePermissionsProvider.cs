#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Xml;

namespace BrightstarDB.Server.AspNetCore.Authorization;

public class StaticStorePermissionsProvider : AbstractStorePermissionsProvider
{
    private const string StoreEl = "store";
    private const string UserEl = "user";
    private const string ClaimEl = "claim";
    private const string PermissionsAttr = "permissions";
    private const string NameAttr = "name";

    private readonly Dictionary<string, Dictionary<string, StorePermissions>> _storeUsers;
    private readonly Dictionary<string, Dictionary<string, StorePermissions>> _storeClaims;

    /// <summary>
    /// Create a new provider with a fixed collection of user and claim permimssions for stores
    /// </summary>
    /// <param name="userPermissions">A dictionary mapping a store name to another dictionary that maps a user name to the permissions for that user on that store.</param>
    /// <param name="claimPermissions">A dictionary mapping a store name to another dictionary that maps a claim to the permissions associated with that claim on that store.</param>
    public StaticStorePermissionsProvider(IDictionary<string, Dictionary<string, StorePermissions>> userPermissions,
                                          IDictionary<string, Dictionary<string, StorePermissions>> claimPermissions)
    {
        _storeUsers = new Dictionary<string, Dictionary<string, StorePermissions>>(userPermissions);
        _storeClaims = new Dictionary<string, Dictionary<string, StorePermissions>>(claimPermissions);
    }

    /// <summary>
    /// Create a new provider that is initialized from an XML configuration.
    /// </summary>
    /// <param name="configEl">The root element for the permissions provider configuration</param>
    public StaticStorePermissionsProvider(XmlNode configEl)
    {
        _storeUsers = new Dictionary<string, Dictionary<string, StorePermissions>>();
        _storeClaims = new Dictionary<string, Dictionary<string, StorePermissions>>();
        if (configEl == null) throw new ArgumentNullException("configEl");
        foreach (var el in configEl.ChildNodes.OfType<XmlElement>().Where(x => x.LocalName.Equals(StoreEl)))
        {
            ProcessStorePermissions(el);
        }
    }

    private void ProcessStorePermissions(XmlElement storePermissionsElement)
    {
        if (!storePermissionsElement.HasAttribute(NameAttr))
        {
            return;
        }

        var storeName = storePermissionsElement.GetAttribute(NameAttr);
        if (!_storeUsers.TryGetValue(storeName, out var storeUsers))
        {
            storeUsers = new Dictionary<string, StorePermissions>();
            _storeUsers[storeName] = storeUsers;
        }
        if (!_storeClaims.TryGetValue(storeName, out var storeClaims))
        {
            storeClaims = new Dictionary<string, StorePermissions>();
            _storeClaims[storeName] = storeClaims;
        }
        foreach (var el in storePermissionsElement.ChildNodes.OfType<XmlElement>())
        {
            if (el.LocalName.Equals(UserEl))
            {
                ProcessPermissionsElement(el, storeUsers);
            }
            else if (el.LocalName.Equals(ClaimEl))
            {
                ProcessPermissionsElement(el, storeClaims);
            }
        }
    }

    private static void ProcessPermissionsElement(XmlElement permissionsElement, IDictionary<string, StorePermissions> permissonsDict)
    {
        if (permissionsElement.HasAttribute(NameAttr) && permissionsElement.HasAttribute(PermissionsAttr) &&
            Enum.TryParse(permissionsElement.GetAttribute(PermissionsAttr), out StorePermissions permissions))
        {
            permissonsDict[permissionsElement.GetAttribute(NameAttr)] = permissions;
        }
    }

    public override StorePermissions GetStorePermissions(ClaimsPrincipal? currentUser, string storeName)
    {
        if (currentUser?.Identity?.IsAuthenticated != true)
        {
            return StorePermissions.None;
        }

        var calculatedPermissions = StorePermissions.None;
        var userName = currentUser.Identity?.Name;
        if (!String.IsNullOrEmpty(userName))
        {
            // See if there are user-specific permissions
            if (_storeUsers.TryGetValue(storeName, out var storeUserPermissions))
            {
                if (storeUserPermissions.TryGetValue(userName, out var userPermissions))
                {
                    calculatedPermissions |= userPermissions;
                }
            }
        }

        if (_storeClaims.TryGetValue(storeName, out var storeClaimPermissions))
        {
            foreach (var claim in currentUser.Claims)
            {
                if (storeClaimPermissions.TryGetValue(claim.Value, out var claimPermissions))
                {
                    calculatedPermissions |= claimPermissions;
                }
            }
        }
        return calculatedPermissions;
    }
}
