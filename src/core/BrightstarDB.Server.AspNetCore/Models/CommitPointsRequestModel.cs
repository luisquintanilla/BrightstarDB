#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models;

public record CommitPointsRequestModel
{
    public string StoreName { get; init; } = null!;
    public int Skip { get; init; }
    public int Take { get; init; }
    public DateTime? Timestamp { get; init; }
    public DateTime? Earliest { get; init; }
    public DateTime? Latest { get; init; }
}
