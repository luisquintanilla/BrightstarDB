#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class SparqlRequestObject
    {
        public string Query { get; set; } = null!;
        public string CommitId { get; set; } = null!;
        public string[] DefaultGraphUri { get; set; } = Array.Empty<string>();
        public string[] NamedGraphUri { get; set; } = Array.Empty<string>();
        public string[] Format { get; set; } = Array.Empty<string>();
    }
}
