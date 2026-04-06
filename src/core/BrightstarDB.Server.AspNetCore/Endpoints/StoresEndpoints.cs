#nullable enable

using System;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class StoresEndpoints
{
    public static RouteGroupBuilder MapStoresEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(string.Empty);
        group.MapGet("/", HandleGet)
            .WithName("ListStores")
            .WithTags("Stores")
            .WithSummary("List all stores")
            .AddSystemPermissionFilter(SystemPermissions.ListStores);
        group.MapPost("/", HandlePost)
            .WithName("CreateStore")
            .WithTags("Stores")
            .WithSummary("Create a new store")
            .AddSystemPermissionFilter(SystemPermissions.CreateStore);
        return group;
    }

    private static Results<Ok<StoresResponseModel>, ProblemHttpResult> HandleGet(IBrightstarService brightstarService)
    {
        try
        {
            return TypedResults.Ok(StoresResponseModel.FromStoreNames(brightstarService.ListStores()));
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static Results<Created<StoreResponseModel>, BadRequest, Conflict, ProblemHttpResult> HandlePost(
        CreateStoreRequestObject? request, IBrightstarService brightstarService)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.StoreName))
        {
            return TypedResults.BadRequest();
        }

        if (brightstarService.DoesStoreExist(request.StoreName))
        {
            return TypedResults.Conflict();
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
            return TypedResults.Created($"/{request.StoreName}", responseModel);
        }
        catch (ArgumentException)
        {
            return TypedResults.BadRequest();
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
