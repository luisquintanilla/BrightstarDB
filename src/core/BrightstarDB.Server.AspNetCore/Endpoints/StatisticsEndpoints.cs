#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class StatisticsEndpoints
{
    public static RouteGroupBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}/statistics");
        group.MapGet("/", HandleGet)
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapGet("/latest", HandleGetLatest)
            .AddStorePermissionFilter(StorePermissions.Read);
        return group;
    }

    private static IResult HandleGet([AsParameters] StatisticsRequestObject request, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(request.StoreName))
            {
                return Results.NotFound();
            }

            var skip = PagingHelpers.NormalizeSkip(request.Skip);
            var take = PagingHelpers.NormalizeTake(request.Take);
            var latest = request.Latest ?? DateTime.MaxValue;
            var earliest = request.Earliest ?? DateTime.MinValue;
            var resourceUri = PagingHelpers.BuildResourceUri(httpContext.Request.Path.Value ?? $"/{request.StoreName}/statistics", new Dictionary<string, string?>
            {
                ["latest"] = request.Latest?.ToString("s"),
                ["earliest"] = request.Earliest?.ToString("s")
            });

            var statistics = brightstarService
                .GetStatistics(request.StoreName, latest, earliest, skip, take + 1)
                .Select(StatisticsResponseModel.From);

            var page = PagingHelpers.ToPage(statistics, take, out var hasNextPage);
            PagingHelpers.AddLinkHeader(httpContext.Response, resourceUri, skip, take, hasNextPage);
            return Results.Ok(page);
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandleGetLatest(string storeName, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return Results.NotFound();
            }

            var latest = brightstarService.GetStatistics(storeName);
            return latest == null
                ? Results.NotFound()
                : Results.Ok(StatisticsResponseModel.From(latest));
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult ServerError(BrightstarClientException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status500InternalServerError);
    }
}
