#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record SparqlRequestObject
{
    public string Query { get; init; } = null!;
    public string CommitId { get; init; } = null!;
    public string[] DefaultGraphUri { get; init; } = [];
    public string[] NamedGraphUri { get; init; } = [];
    public string[] Format { get; init; } = [];
}
