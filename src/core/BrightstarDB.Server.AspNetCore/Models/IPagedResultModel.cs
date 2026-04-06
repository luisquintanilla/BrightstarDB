#nullable enable

namespace BrightstarDB.Server.AspNetCore.Models;

public interface IPagedResultModel
{
    string FirstPageLink { get; set; }
    string PreviousPageLink { get; set; }
    string NextPageLink { get; set; }
    dynamic RequestProperties { get; set; }
}
