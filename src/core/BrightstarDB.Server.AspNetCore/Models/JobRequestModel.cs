#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class JobRequestModel
    {
        public string StoreName { get; set; } = null!;
        public string JobId { get; set; } = null!;
    }
}
