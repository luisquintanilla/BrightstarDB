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

public static class CommitPointsEndpoints
{
    public static RouteGroupBuilder MapCommitPointsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}/commits");
        group.MapGet("/", HandleGet)
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapPost("/", HandlePost)
            .AddStorePermissionFilter(StorePermissions.Admin);
        return group;
    }

    private static IResult HandleGet([AsParameters] CommitPointsRequestModel request, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(request.StoreName))
            {
                return Results.NotFound();
            }

            var skip = PagingHelpers.NormalizeSkip(request.Skip);
            var take = PagingHelpers.NormalizeTake(request.Take);
            if (request.Timestamp.HasValue)
            {
                var commitPoint = brightstarService.GetCommitPoint(request.StoreName, request.Timestamp.Value);
                return commitPoint == null
                    ? Results.NotFound()
                    : Results.Ok(CommitPointResponseModel.From(commitPoint));
            }

            var resourcePath = httpContext.Request.Path.Value ?? $"/{request.StoreName}/commits";
            if (request.Earliest.HasValue && request.Latest.HasValue)
            {
                var resourceUri = PagingHelpers.BuildResourceUri(resourcePath, new Dictionary<string, string?>
                {
                    ["latest"] = request.Latest.Value.ToString("s"),
                    ["earliest"] = request.Earliest.Value.ToString("s")
                });

                var commits = brightstarService
                    .GetCommitPoints(request.StoreName, request.Latest.Value, request.Earliest.Value, skip, take + 1)
                    .Select(CommitPointResponseModel.From);

                var page = PagingHelpers.ToPage(commits, take, out var hasNextPage);
                PagingHelpers.AddLinkHeader(httpContext.Response, resourceUri, skip, take, hasNextPage);
                return Results.Ok(page);
            }

            var commitPoints = brightstarService
                .GetCommitPoints(request.StoreName, skip, take + 1)
                .Select(CommitPointResponseModel.From);

            var pagedCommitPoints = PagingHelpers.ToPage(commitPoints, take, out var hasNext);
            PagingHelpers.AddLinkHeader(httpContext.Response, resourcePath, skip, take, hasNext);
            return Results.Ok(pagedCommitPoints);
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandlePost(string storeName, CommitPointResponseModel? commitPoint, IBrightstarService brightstarService)
    {
        if (commitPoint == null || string.IsNullOrWhiteSpace(commitPoint.StoreName) ||
            !string.Equals(commitPoint.StoreName, storeName, StringComparison.Ordinal))
        {
            return Results.BadRequest();
        }

        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return Results.NotFound();
            }

            var commitPointInfo = brightstarService.GetCommitPoint(storeName, commitPoint.Id);
            if (commitPointInfo == null)
            {
                return Results.BadRequest();
            }

            brightstarService.RevertToCommitPoint(storeName, commitPointInfo);
            return Results.Ok();
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
