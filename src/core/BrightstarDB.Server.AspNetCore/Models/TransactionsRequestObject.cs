#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models
{
    public class TransactionsRequestObject
    {
        public string StoreName { get; set; } = null!;
        public int Skip { get; set; }
        public int Take { get; set; }
    }
}
