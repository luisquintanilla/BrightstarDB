#nullable enable
using System;

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class StatisticsRequestObject
    {
        public string StoreName { get; set; } = null!;
        public DateTime? Earliest { get; set; }
        public DateTime? Latest { get; set; }
        public int Skip { get; set; }
        public int Take { get; set; }
    }
}
