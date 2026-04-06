#nullable enable

using System.Linq;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class StoreEndpoints
{
    public static RouteGroupBuilder MapStoreEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}");
        group.MapGet(string.Empty, HandleGet)
            .WithName("GetStore")
            .WithTags("Store")
            .WithSummary("Get store details")
            .AddStorePermissionFilter(StorePermissions.Read)
            .CacheOutput("store-reads");
        group.MapMethods(string.Empty, [HttpMethods.Head], HandleHead)
            .WithName("HeadStore")
            .WithTags("Store")
            .WithSummary("Check store existence")
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapDelete(string.Empty, HandleDelete)
            .WithName("DeleteStore")
            .WithTags("Store")
            .WithSummary("Delete a store")
            .AddStorePermissionFilter(StorePermissions.Admin);
        return group;
    }

    private static Results<Ok<StoreResponseModel>, NotFound, ProblemHttpResult> HandleGet(
        string storeName, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return TypedResults.NotFound();
            }

            var commitPoints = brightstarService.GetCommitPoints(storeName, 0, PagingHelpers.DefaultPageSize);
            return TypedResults.Ok(StoreResponseModel.FromStore(storeName, commitPoints));
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static Results<Ok, NotFound, ProblemHttpResult> HandleHead(
        string storeName, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return TypedResults.NotFound();
            }

            var latestCommit = brightstarService.GetCommitPoints(storeName, 0, 1).FirstOrDefault();
            if (latestCommit != null)
            {
                httpContext.Response.Headers.LastModified = latestCommit.CommitTime.ToUniversalTime().ToString("r");
            }

            return TypedResults.Ok();
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static Results<Ok<StoreDeletedModel>, ProblemHttpResult> HandleDelete(
        string storeName, IBrightstarService brightstarService)
    {
        try
        {
            if (brightstarService.DoesStoreExist(storeName))
            {
                brightstarService.DeleteStore(storeName);
            }

            return TypedResults.Ok(new StoreDeletedModel { StoreName = storeName });
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
