#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class SparqlUpdateRequestObject
    {
        public string StoreName { get; set; } = null!;
        public string Update { get; set; } = null!;
        public string[] UsingGraphUri { get; set; } = Array.Empty<string>();
        public string[] UsingNamedGraphUri { get; set; } = Array.Empty<string>();
    }
}
