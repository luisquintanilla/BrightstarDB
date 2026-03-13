#nullable enable

using System;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class StoresEndpoints
{
    public static RouteGroupBuilder MapStoresEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(string.Empty);
        group.MapGet("/", HandleGet)
            .AddSystemPermissionFilter(SystemPermissions.ListStores);
        group.MapPost("/", HandlePost)
            .AddSystemPermissionFilter(SystemPermissions.CreateStore);
        return group;
    }

    private static IResult HandleGet(IBrightstarService brightstarService)
    {
        try
        {
            return Results.Ok(StoresResponseModel.FromStoreNames(brightstarService.ListStores()));
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandlePost(CreateStoreRequestObject? request, IBrightstarService brightstarService)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.StoreName))
        {
            return Results.BadRequest();
        }

        if (brightstarService.DoesStoreExist(request.StoreName))
        {
            return Results.Conflict();
        }

        try
        {
            var persistenceType = request.GetBrightstarPersistenceType();
            if (persistenceType.HasValue)
            {
                brightstarService.CreateStore(request.StoreName, persistenceType.Value);
            }
            else
            {
                brightstarService.CreateStore(request.StoreName);
            }

            var responseModel = StoreResponseModel.FromStore(request.StoreName);
            return Results.Created($"/{request.StoreName}", responseModel);
        }
        catch (ArgumentException)
        {
            return Results.BadRequest();
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
