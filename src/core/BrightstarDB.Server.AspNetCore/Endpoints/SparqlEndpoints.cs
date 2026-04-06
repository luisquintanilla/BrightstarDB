#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrightstarDB;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Formatters;
using BrightstarDB.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using VDS.RDF.Parsing;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class SparqlEndpoints
{
    private const string SparqlQueryMediaType = "application/sparql-query";

    public static IEndpointRouteBuilder MapSparqlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{storeName}/sparql", HandleStoreQuery)
            .WithName("SparqlQueryGet")
            .WithTags("SPARQL")
            .WithSummary("Execute a SPARQL query via GET");
        endpoints.MapPost("/{storeName}/sparql", HandleStoreQuery)
            .WithName("SparqlQueryPost")
            .WithTags("SPARQL")
            .WithSummary("Execute a SPARQL query via POST");
        endpoints.MapGet("/{storeName}/commits/{commitId}/sparql", HandleCommitQuery)
            .WithName("SparqlCommitQueryGet")
            .WithTags("SPARQL")
            .WithSummary("Execute a SPARQL query against a specific commit via GET");
        endpoints.MapPost("/{storeName}/commits/{commitId}/sparql", HandleCommitQuery)
            .WithName("SparqlCommitQueryPost")
            .WithTags("SPARQL")
            .WithSummary("Execute a SPARQL query against a specific commit via POST");
        return endpoints;
    }

    private static async Task<IResult> HandleStoreQuery(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.Read))
        {
            return TypedResults.Unauthorized();
        }

        var request = await BindSparqlRequestAsync(httpContext.Request);
        if (string.IsNullOrWhiteSpace(request.Query)) return TypedResults.BadRequest();

        SerializableModel resultModel;
        try
        {
            resultModel = SparqlQueryHelper.GetResultModel(request.Query);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
        catch (RdfParseException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }

        var selection = SparqlResultFormatHelper.Resolve(httpContext.Request, resultModel, request.FormatOverrides);
        return new SparqlQueryResult(
            brightstarService,
            storeName,
            null,
            request.Query,
            request.DefaultGraphUris,
            selection.ResultsFormat,
            selection.GraphFormat);
    }

    private static async Task<IResult> HandleCommitQuery(
        string storeName,
        string commitId,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.Read))
        {
            return TypedResults.Unauthorized();
        }

        if (!ulong.TryParse(commitId, out var parsedCommitId)) return TypedResults.BadRequest();

        var request = await BindSparqlRequestAsync(httpContext.Request);
        if (string.IsNullOrWhiteSpace(request.Query)) return TypedResults.BadRequest();

        SerializableModel resultModel;
        try
        {
            resultModel = SparqlQueryHelper.GetResultModel(request.Query);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
        catch (RdfParseException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }

        var selection = SparqlResultFormatHelper.Resolve(httpContext.Request, resultModel, request.FormatOverrides);
        return new SparqlQueryResult(
            brightstarService,
            storeName,
            parsedCommitId,
            request.Query,
            request.DefaultGraphUris,
            selection.ResultsFormat,
            selection.GraphFormat);
    }

    private static async Task<SparqlRequestData> BindSparqlRequestAsync(HttpRequest request)
    {
        string? query = request.Query["query"].FirstOrDefault();
        var defaultGraphUris = SparqlResultFormatHelper.ReadValues(request.Query, "default-graph-uri").ToArray();
        var formatOverrides = SparqlResultFormatHelper.ReadValues(request.Query, "format").ToArray();

        if (HttpMethods.IsPost(request.Method))
        {
            if (IsContentType(request.ContentType, SparqlQueryMediaType))
            {
                using var reader = new StreamReader(request.Body);
                query = await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
            }
            else if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
                query = form["query"].FirstOrDefault();
                defaultGraphUris = SparqlResultFormatHelper.ReadValues(form, "default-graph-uri").ToArray();
                formatOverrides = SparqlResultFormatHelper.ReadValues(form, "format").ToArray();
            }
        }

        return new SparqlRequestData(query, defaultGraphUris, formatOverrides);
    }

    private static bool IsContentType(string? contentType, string expectedMediaType)
    {
        return contentType != null && contentType.StartsWith(expectedMediaType, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record SparqlRequestData(string? Query, IReadOnlyList<string> DefaultGraphUris, IReadOnlyList<string> FormatOverrides);
}
