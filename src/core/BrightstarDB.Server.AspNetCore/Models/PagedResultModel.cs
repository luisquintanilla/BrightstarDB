#nullable enable
using System;
using System.Collections.Generic;

namespace BrightstarDB.Server.AspNetCore.Models;

public class PagedResultModel<T> : IPagedResultModel
{
    public PagedResultModel(string linkFirst, string linkPrev, string linkNext, List<T> returnItems, dynamic requestProperties)
    {
        FirstPageLink = linkFirst;
        PreviousPageLink = linkPrev;
        NextPageLink = linkNext;
        Items = returnItems;
        RequestProperties = requestProperties;
    }

    public string FirstPageLink { get; set; } = null!;
    public bool HasFirstPageLink => !String.IsNullOrEmpty(FirstPageLink);
    public string PreviousPageLink { get; set; } = null!;
    public bool HasPreviousPageLink => !String.IsNullOrEmpty(PreviousPageLink);
    public string NextPageLink { get; set; } = null!;
    public bool HasNextPageLink => !String.IsNullOrEmpty(NextPageLink);
    public dynamic RequestProperties { get; set; } = null!;
    public List<T> Items { get; set; } = null!;
}
