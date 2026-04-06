#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record TransactionsRequestObject
{
    public string StoreName { get; init; } = null!;
    public int Skip { get; init; }
    public int Take { get; init; }
}
