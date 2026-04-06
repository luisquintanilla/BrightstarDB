#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record JobRequestModel
{
    public string StoreName { get; init; } = null!;
    public string JobId { get; init; } = null!;
}
