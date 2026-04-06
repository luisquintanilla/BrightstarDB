#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record JobsRequestModel
{
    public string StoreName { get; init; } = null!;
    public int Skip { get; init; }
    public int Take { get; init; }
}
