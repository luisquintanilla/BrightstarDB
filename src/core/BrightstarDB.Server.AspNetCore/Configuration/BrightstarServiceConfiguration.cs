#nullable enable

using System.Collections.Generic;

namespace BrightstarDB.Server.AspNetCore.Configuration;

public sealed class BrightstarServiceConfiguration
{
    public const string SectionName = "BrightstarService";

    public string? ConnectionString { get; set; }

    public StorePermissionsConfiguration StorePermissions { get; set; } = new();

    public SystemPermissionsConfiguration SystemPermissions { get; set; } = new();

    public AuthenticationConfiguration Authentication { get; set; } = new();

    public CorsConfiguration Cors { get; set; } = new();
}

public sealed class StorePermissionsConfiguration
{
    public string Authenticated { get; set; } = "None";

    public string Anonymous { get; set; } = "None";
}

public sealed class SystemPermissionsConfiguration
{
    public string Authenticated { get; set; } = "None";

    public string Anonymous { get; set; } = "None";
}

public sealed class AuthenticationConfiguration
{
    public string Type { get; set; } = "None";

    public string BasicAuthRealm { get; set; } = "BrightstarDB";

    public ICollection<BasicAuthenticationUser> Credentials { get; set; } = new List<BasicAuthenticationUser>();

    public string Realm
    {
        get => BasicAuthRealm;
        set => BasicAuthRealm = value;
    }

    public ICollection<BasicAuthenticationUser> Users
    {
        get => Credentials;
        set => Credentials = value;
    }
}

public sealed class BasicAuthenticationUser
{
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public ICollection<string> Claims { get; set; } = new List<string>();
}
