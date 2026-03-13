#nullable enable

using System.Collections.Generic;

namespace BrightstarDB.Server.AspNetCore.Configuration;

public sealed class CorsConfiguration
{
    public bool DisableCors { get; set; }

    public bool Disabled
    {
        get => DisableCors;
        set => DisableCors = value;
    }

    public string AllowOrigin { get; set; } = "*";

    public ICollection<string> AllowedOrigins { get; set; } = new List<string>();

    public ICollection<string> AllowedHeaders { get; set; } = new List<string>();

    public ICollection<string> AllowedMethods { get; set; } = new List<string>();

    public ICollection<string> ExposedHeaders { get; set; } = new List<string>();

    public bool AllowCredentials { get; set; }
}
