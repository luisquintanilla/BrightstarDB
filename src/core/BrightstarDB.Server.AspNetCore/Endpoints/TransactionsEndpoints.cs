#nullable enable

using System;
using System.Linq;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class TransactionsEndpoints
{
    public static RouteGroupBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}/transactions");
        group.MapGet("/", HandleGet)
            .AddStorePermissionFilter(StorePermissions.ViewHistory);
        group.MapGet("/byjob/{jobId}", HandleGetByJob)
            .AddStorePermissionFilter(StorePermissions.ViewHistory);
        return group;
    }

    private static IResult HandleGet([AsParameters] TransactionsRequestObject request, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(request.StoreName))
            {
                return Results.NotFound();
            }

            var skip = PagingHelpers.NormalizeSkip(request.Skip);
            var take = PagingHelpers.NormalizeTake(request.Take);
            var transactions = brightstarService
                .GetTransactions(request.StoreName, skip, take + 1)
                .Select(TransactionResponseModel.From);

            var page = PagingHelpers.ToPage(transactions, take, out var hasNextPage);
            var resourceUri = httpContext.Request.Path.Value ?? $"/{request.StoreName}/transactions";
            PagingHelpers.AddLinkHeader(httpContext.Response, resourceUri, skip, take, hasNextPage);
            return Results.Ok(page);
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static IResult HandleGetByJob(string storeName, string jobId, IBrightstarService brightstarService)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            return Results.NotFound();
        }

        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return Results.NotFound();
            }

            var transaction = brightstarService.GetTransaction(storeName, parsedJobId);
            return transaction == null
                ? Results.NotFound()
                : Results.Ok(TransactionResponseModel.From(transaction));
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
