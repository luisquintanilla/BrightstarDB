#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BrightstarDB.Client;
using BrightstarDB.Dto;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using BrightstarDB.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class JobsEndpoints
{
    private const int DefaultPageSize = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{storeName}/jobs", HandleListJobs)
            .WithName("ListJobs")
            .WithTags("Jobs")
            .WithSummary("List jobs for a store");
        endpoints.MapGet("/{storeName}/jobs/{jobId}", HandleGetJob)
            .WithName("GetJob")
            .WithTags("Jobs")
            .WithSummary("Get job status by ID");
        endpoints.MapPost("/{storeName}/jobs", HandleCreateJob)
            .WithName("CreateJob")
            .WithTags("Jobs")
            .WithSummary("Create a new job");
        return endpoints;
    }

    private static Results<Ok<List<JobResponseModel>>, NotFound, UnauthorizedHttpResult> HandleListJobs(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.Read))
        {
            return TypedResults.Unauthorized();
        }

        if (!brightstarService.DoesStoreExist(storeName)) return TypedResults.NotFound();

        var skip = ReadNonNegativeInt(httpContext.Request.Query["skip"], 0);
        var take = ReadNonNegativeInt(httpContext.Request.Query["take"], DefaultPageSize);
        if (take <= 0) take = DefaultPageSize;

        var jobs = brightstarService.GetJobInfo(storeName, skip, take + 1)
            .Select(job => job.ToResponseModel(storeName))
            .ToList();

        AddPagingLinks(httpContext.Response, httpContext.Request.Path.Value ?? $"/{storeName}/jobs", skip, take, DefaultPageSize, jobs.Count > take);
        return TypedResults.Ok(jobs.Take(take).ToList());
    }

    private static Results<Ok<JobResponseModel>, NotFound, UnauthorizedHttpResult> HandleGetJob(
        string storeName,
        string jobId,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.Read))
        {
            return TypedResults.Unauthorized();
        }

        if (!brightstarService.DoesStoreExist(storeName)) return TypedResults.NotFound();

        var job = brightstarService.GetJobInfo(storeName, jobId);
        return job == null ? TypedResults.NotFound() : TypedResults.Ok(job.ToResponseModel(storeName));
    }

    private static async Task<Results<Created<JobResponseModel>, BadRequest, BadRequest<object>, NotFound, UnauthorizedHttpResult>> HandleCreateJob(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!brightstarService.DoesStoreExist(storeName)) return TypedResults.NotFound();

        JobRequestObject? jobRequest;
        try
        {
            jobRequest = await JsonSerializer.DeserializeAsync<JobRequestObject>(httpContext.Request.Body, JsonOptions, httpContext.RequestAborted);
        }
        catch (JsonException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }

        if (jobRequest == null || string.IsNullOrWhiteSpace(jobRequest.JobType)) return TypedResults.BadRequest();

        var parameters = jobRequest.JobParameters == null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(jobRequest.JobParameters, StringComparer.OrdinalIgnoreCase);

        try
        {
            var queuedJobInfo = CreateJob(httpContext, permissionsProvider, brightstarService, storeName, jobRequest, parameters);
            var responseModel = queuedJobInfo.ToResponseModel(storeName);
            return TypedResults.Created($"{storeName}/jobs/{queuedJobInfo.JobId}", responseModel);
        }
        catch (BadJobRequestException)
        {
            return TypedResults.BadRequest();
        }
        catch (UnauthorizedAccessException)
        {
            return TypedResults.Unauthorized();
        }
        catch (NoSuchStoreException)
        {
            return TypedResults.NotFound();
        }
        catch (BrightstarClientException ex)
        {
            return TypedResults.BadRequest<object>(new { error = ex.Message });
        }
    }

    private static IJobInfo CreateJob(
        HttpContext httpContext,
        AbstractStorePermissionsProvider permissionsProvider,
        IBrightstarService brightstarService,
        string storeName,
        JobRequestObject jobRequest,
        IReadOnlyDictionary<string, string?> parameters)
    {
        var label = jobRequest.Label;
        switch (jobRequest.JobType.Trim().ToLowerInvariant())
        {
            case "consolidate":
            case "consolidatestore":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.Admin);
                return brightstarService.ConsolidateStore(storeName, label);

            case "createsnapshot":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.Admin);
                if (!TryGetRequired(parameters, "TargetStoreName", out var targetStoreName) ||
                    !TryGetRequired(parameters, "PersistenceType", out var persistenceTypeValue) ||
                    !Enum.TryParse<PersistenceType>(persistenceTypeValue, true, out var persistenceType))
                {
                    throw new BadJobRequestException();
                }

                ICommitPointInfo? commitPoint = null;
                if (TryGetOptional(parameters, "CommitId", out var commitIdValue))
                {
                    if (!ulong.TryParse(commitIdValue, out var commitId)) throw new BadJobRequestException();
                    commitPoint = brightstarService.GetCommitPoint(storeName, commitId);
                    if (commitPoint == null) throw new BadJobRequestException();
                }

                return brightstarService.CreateSnapshot(storeName, targetStoreName!, persistenceType, commitPoint, label);

            case "export":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.Export);
                if (!TryGetRequired(parameters, "FileName", out var exportFileName)) throw new BadJobRequestException();
                var exportFormat = TryGetOptional(parameters, "Format", out var exportFormatValue)
                    ? RdfFormat.GetResultsFormat(exportFormatValue!) ?? RdfFormat.NQuads
                    : RdfFormat.NQuads;
                return brightstarService.StartExport(
                    storeName,
                    exportFileName!,
                    TryGetOptional(parameters, "GraphUri", out var graphUri) ? graphUri : null,
                    exportFormat,
                    label);

            case "import":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.TransactionUpdate);
                if (!TryGetRequired(parameters, "FileName", out var importFileName)) throw new BadJobRequestException();
                RdfFormat? importFormat = null;
                if (TryGetOptional(parameters, "ImportFormat", out var importFormatValue))
                {
                    importFormat = RdfFormat.GetResultsFormat(importFormatValue!);
                    if (importFormat == null) throw new BadJobRequestException();
                }

                return brightstarService.StartImport(
                    storeName,
                    importFileName!,
                    TryGetOptional(parameters, "DefaultGraphUri", out var defaultGraphUri) ? defaultGraphUri! : Constants.DefaultGraphUri,
                    label,
                    importFormat);

            case "repeattransaction":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.Admin);
                if (!TryGetRequired(parameters, "JobId", out var repeatJobIdValue) || !Guid.TryParse(repeatJobIdValue, out var repeatJobId))
                {
                    throw new BadJobRequestException();
                }

                var transaction = brightstarService.GetTransaction(storeName, repeatJobId);
                if (transaction == null) throw new BadJobRequestException();
                return brightstarService.ReExecuteTransaction(storeName, transaction, label);

            case "sparqlupdate":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.SparqlUpdate);
                if (!TryGetRequired(parameters, "UpdateExpression", out var updateExpression)) throw new BadJobRequestException();
                return brightstarService.ExecuteUpdate(storeName, updateExpression!, false, label);

            case "transaction":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.TransactionUpdate);
                return brightstarService.ExecuteTransaction(
                    storeName,
                    new UpdateTransactionData
                    {
                        ExistencePreconditions = TryGetOptional(parameters, "Preconditions", out var preconditions) ? preconditions : null,
                        NonexistencePreconditions = TryGetOptional(parameters, "NonexistencePreconditions", out var nonexistence) ? nonexistence : null,
                        DeletePatterns = TryGetOptional(parameters, "Deletes", out var deletes) ? deletes : null,
                        InsertData = TryGetOptional(parameters, "Inserts", out var inserts) ? inserts : null,
                        DefaultGraphUri = TryGetOptional(parameters, "DefaultGraphUri", out var txnDefaultGraph) ? txnDefaultGraph : null
                    },
                    false,
                    label);

            case "updatestats":
            case "updatestatistics":
                AssertPermission(httpContext, permissionsProvider, storeName, StorePermissions.Admin);
                return brightstarService.UpdateStatistics(storeName, label);

            default:
                throw new BadJobRequestException();
        }
    }

    private static void AssertPermission(
        HttpContext httpContext,
        AbstractStorePermissionsProvider permissionsProvider,
        string storeName,
        StorePermissions permissionRequired)
    {
        var permissions = permissionsProvider.GetStorePermissions(httpContext.User, storeName);
        if ((permissions & permissionRequired) != permissionRequired)
        {
            throw new UnauthorizedAccessException();
        }
    }

    private static bool TryGetRequired(IReadOnlyDictionary<string, string?> parameters, string key, out string? value)
    {
        if (TryGetOptional(parameters, key, out value)) return true;
        value = null;
        return false;
    }

    private static bool TryGetOptional(IReadOnlyDictionary<string, string?> parameters, string key, out string? value)
    {
        if (parameters.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value)) return true;
        value = null;
        return false;
    }

    private static int ReadNonNegativeInt(Microsoft.Extensions.Primitives.StringValues values, int defaultValue)
    {
        return int.TryParse(values.FirstOrDefault(), out var parsed) && parsed >= 0 ? parsed : defaultValue;
    }

    private static void AddPagingLinks(HttpResponse response, string resourceUri, int skip, int take, int defaultPageSize, bool hasNextPage)
    {
        var links = new List<string>();
        var querySeparator = resourceUri.Contains('?') ? "&" : "?";
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

    private sealed class BadJobRequestException : Exception
    {
    }
}
