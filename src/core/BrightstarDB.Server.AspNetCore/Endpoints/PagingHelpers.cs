#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class PagingHelpers
{
    public const int DefaultPageSize = 10;

    public static int NormalizeSkip(int skip)
    {
        return skip < 0 ? 0 : skip;
    }

    public static int NormalizeTake(int take)
    {
        return take > 0 ? take : DefaultPageSize;
    }

    public static IReadOnlyList<T> ToPage<T>(IEnumerable<T> items, int take, out bool hasNextPage)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.ToList();
        hasNextPage = itemList.Count > take;
        return hasNextPage ? itemList.Take(take).ToList() : itemList;
    }

    public static string BuildResourceUri(string path, IReadOnlyDictionary<string, string?>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        if (parameters == null || parameters.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&",
            parameters
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}"));

        return string.IsNullOrEmpty(queryString) ? path : $"{path}?{queryString}";
    }

    public static void AddLinkHeader(HttpResponse response, string resourceUri, int skip, int take, bool hasNextPage)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(resourceUri);

        var links = new List<string>();
        var querySeparator = resourceUri.Contains('?', StringComparison.Ordinal) ? "&" : "?";

        if (skip > 0)
        {
            links.Add($"<{resourceUri}>;rel=first");
            var previousPage = skip - take;
            var previousLink = previousPage <= 0 ? resourceUri : $"{resourceUri}{querySeparator}skip={previousPage}";
            links.Add($"<{previousLink}>;rel=prev");
        }

        if (hasNextPage)
        {
            links.Add($"<{resourceUri}{querySeparator}skip={skip + take}>;rel=next");
        }

        if (links.Count > 0)
        {
            response.Headers.Link = string.Join(',', links);
        }
    }
}
