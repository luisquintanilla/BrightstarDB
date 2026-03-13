#nullable enable

using System.Collections.Generic;
using System.Linq;

namespace BrightstarDB.Server.AspNetCore.Models;

public class StoresResponseModel
{
    public List<string> Stores { get; set; } = new();

    public static StoresResponseModel FromStoreNames(IEnumerable<string> storeNames)
    {
        return new StoresResponseModel
        {
            Stores = storeNames.ToList()
        };
    }
}
