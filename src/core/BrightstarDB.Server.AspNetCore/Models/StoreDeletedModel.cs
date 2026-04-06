#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record StoreDeletedModel
{
    public string StoreName { get; init; } = null!;
}
