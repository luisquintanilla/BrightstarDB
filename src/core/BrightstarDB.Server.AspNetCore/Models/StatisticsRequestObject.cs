#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models;

public record StatisticsRequestObject
{
    public string StoreName { get; init; } = null!;
    public DateTime? Earliest { get; init; }
    public DateTime? Latest { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
}
