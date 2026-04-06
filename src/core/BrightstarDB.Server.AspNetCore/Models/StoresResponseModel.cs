#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace BrightstarDB.Server.AspNetCore.Models;

public record StoresResponseModel
{
    public List<string> Stores { get; init; } = [];

    public static StoresResponseModel FromStoreNames(IEnumerable<string> storeNames)
    {
        return new StoresResponseModel
        {
            Stores = storeNames.ToList()
        };
    }
}
