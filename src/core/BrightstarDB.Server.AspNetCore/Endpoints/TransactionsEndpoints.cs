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

public static class TransactionsEndpoints
{
    public static RouteGroupBuilder MapTransactionsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/{storeName}/transactions");
        group.MapGet("/", HandleGet)
            .WithName("ListTransactions")
            .WithTags("Transactions")
            .WithSummary("List transactions for a store")
            .AddStorePermissionFilter(StorePermissions.ViewHistory);
        group.MapGet("/byjob/{jobId}", HandleGetByJob)
            .WithName("GetTransactionByJob")
            .WithTags("Transactions")
            .WithSummary("Get transaction by job ID")
            .AddStorePermissionFilter(StorePermissions.ViewHistory);
        return group;
    }

    private static Results<Ok<IReadOnlyList<TransactionResponseModel>>, NotFound, ProblemHttpResult> HandleGet(
        [AsParameters] TransactionsRequestObject request, HttpContext httpContext, IBrightstarService brightstarService)
    {
        try
        {
            if (!brightstarService.DoesStoreExist(request.StoreName))
            {
                return TypedResults.NotFound();
            }

            var skip = PagingHelpers.NormalizeSkip(request.Skip);
            var take = PagingHelpers.NormalizeTake(request.Take);
            var transactions = brightstarService
                .GetTransactions(request.StoreName, skip, take + 1)
                .Select(TransactionResponseModel.From);

            var page = PagingHelpers.ToPage(transactions, take, out var hasNextPage);
            var resourceUri = httpContext.Request.Path.Value ?? $"/{request.StoreName}/transactions";
            PagingHelpers.AddLinkHeader(httpContext.Response, resourceUri, skip, take, hasNextPage);
            return TypedResults.Ok(page);
        }
        catch (BrightstarClientException ex)
        {
            return ServerError(ex);
        }
    }

    private static Results<Ok<TransactionResponseModel>, NotFound, ProblemHttpResult> HandleGetByJob(
        string storeName, string jobId, IBrightstarService brightstarService)
    {
        if (!Guid.TryParse(jobId, out var parsedJobId))
        {
            return TypedResults.NotFound();
        }

        try
        {
            if (!brightstarService.DoesStoreExist(storeName))
            {
                return TypedResults.NotFound();
            }

            var transaction = brightstarService.GetTransaction(storeName, parsedJobId);
            return transaction == null
                ? TypedResults.NotFound()
                : TypedResults.Ok(TransactionResponseModel.From(transaction));
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
