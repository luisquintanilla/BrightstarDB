#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record StoreDeletedModel
{
    public required string StoreName { get; init; }
}
