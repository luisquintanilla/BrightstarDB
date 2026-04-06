#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public record SparqlUpdateRequestObject
{
    public string StoreName { get; init; } = null!;
    public string Update { get; init; } = null!;
    public string[] UsingGraphUri { get; init; } = [];
    public string[] UsingNamedGraphUri { get; init; } = [];
}
