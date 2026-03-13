#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class CommitPointsRequestModel
    {
        public string StoreName { get; set; } = null!;
        public int Skip { get; set; }
        public int Take { get; set; }
        public DateTime? Timestamp { get; set; }
        public DateTime? Earliest { get; set; }
        public DateTime? Latest { get; set; }
    }
}
