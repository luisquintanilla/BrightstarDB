#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class CommitPointsEndpoints
{
    public static RouteGroupBuilder MapCommitPointsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}/commits");
        group.MapGet("/", HandleGet)
            .WithName("ListCommitPoints")
            .WithTags("CommitPoints")
            .WithSummary("List commit points for a store")
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapPost("/", HandlePost)
            .WithName("RevertToCommitPoint")
            .WithTags("CommitPoints")
            .WithSummary("Revert store to a specific commit point")
            .AddStorePermissionFilter(StorePermissions.Admin);
        return group;
    }

    private static Results<Ok<CommitPointResponseModel>, Ok<IReadOnlyList<CommitPointResponseModel>>, NotFound, ProblemHttpResult> HandleGet(
        [AsParameters] CommitPointsRequestModel request, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(request.StoreName))
            {
                return TypedResults.NotFound();
            }

            var skip = PagingHelpers.NormalizeSkip(request.Skip);
            var take = PagingHelpers.NormalizeTake(request.Take);
            if (request.Timestamp.HasValue)
            {
                var commitPoint = brightstarService.GetCommitPoint(request.StoreName, request.Timestamp.Value);
                return commitPoint == null
                    ? TypedResults.NotFound()
                    : TypedResults.Ok(CommitPointResponseModel.From(commitPoint));
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
                return TypedResults.Ok(page);
            }

            var commitPoints = brightstarService
                .GetCommitPoints(request.StoreName, skip, take + 1)
                .Select(CommitPointResponseModel.From);

            var pagedCommitPoints = PagingHelpers.ToPage(commitPoints, take, out var hasNext);
            PagingHelpers.AddLinkHeader(httpContext.Response, resourcePath, skip, take, hasNext);
            return TypedResults.Ok(pagedCommitPoints);
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static Results<Ok, NotFound, BadRequest, ProblemHttpResult> HandlePost(
        string storeName, CommitPointResponseModel? commitPoint, IBrightstarService brightstarService)
    {
        if (commitPoint == null || string.IsNullOrWhiteSpace(commitPoint.StoreName) ||
            !string.Equals(commitPoint.StoreName, storeName, StringComparison.Ordinal))
        {
            return TypedResults.BadRequest();
        }

        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return TypedResults.NotFound();
            }

            var commitPointInfo = brightstarService.GetCommitPoint(storeName, commitPoint.Id);
            if (commitPointInfo == null)
            {
                return TypedResults.BadRequest();
            }

            brightstarService.RevertToCommitPoint(storeName, commitPointInfo);
            return TypedResults.Ok();
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static ProblemHttpResult ServerError(BrightstarClientException ex)
    {
        return TypedResults.Problem(detail: ex.Message, statusCode: StatusCodes.Status500InternalServerError);
    }
}
