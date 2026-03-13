#nullable enable

using System.Linq;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class StoreEndpoints
{
    public static RouteGroupBuilder MapStoreEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}");
        group.MapGet(string.Empty, HandleGet)
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapMethods(string.Empty, new[] { HttpMethods.Head }, HandleHead)
            .AddStorePermissionFilter(StorePermissions.Read);
        group.MapDelete(string.Empty, HandleDelete)
            .AddStorePermissionFilter(StorePermissions.Admin);
        return group;
    }

    private static IResult HandleGet(string storeName, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return Results.NotFound();
            }

            var commitPoints = brightstarService.GetCommitPoints(storeName, 0, PagingHelpers.DefaultPageSize);
            return Results.Ok(StoreResponseModel.FromStore(storeName, commitPoints));
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandleHead(string storeName, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return Results.NotFound();
            }

            var latestCommit = brightstarService.GetCommitPoints(storeName, 0, 1).FirstOrDefault();
            if (latestCommit != null)
            {
                httpContext.Response.Headers.LastModified = latestCommit.CommitTime.ToUniversalTime().ToString("r");
            }

            return Results.Ok();
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandleDelete(string storeName, IBrightstarService brightstarService)
    {
        try
        {
            if (brightstarService.DoesStoreExist(storeName))
            {
                brightstarService.DeleteStore(storeName);
            }

            return Results.Ok(new StoreDeletedModel { StoreName = storeName });
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
