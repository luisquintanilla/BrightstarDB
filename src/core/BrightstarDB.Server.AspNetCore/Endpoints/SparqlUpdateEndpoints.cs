#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Update;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class SparqlUpdateEndpoints
{
    private const string SparqlUpdateMediaType = "application/sparql-update";

    public static IEndpointRouteBuilder MapSparqlUpdateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/{storeName}/update", HandleUpdate);
        return endpoints;
    }

    private static async Task<IResult> HandleUpdate(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.SparqlUpdate))
        {
            return Results.Unauthorized();
        }

        if (!brightstarService.DoesStoreExist(storeName)) return Results.NotFound();

        var updateText = await ReadUpdateTextAsync(httpContext.Request);
        if (string.IsNullOrWhiteSpace(updateText)) return Results.BadRequest();

        try
        {
            _ = new SparqlUpdateParser().ParseFromString(updateText);
        }
        catch (RdfParseException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (RdfException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        try
        {
            var jobInfo = brightstarService.ExecuteUpdate(storeName, updateText, true);
            var responseModel = jobInfo.ToResponseModel(storeName);
            return jobInfo.JobCompletedOk ? Results.Ok(responseModel) : Results.BadRequest(responseModel);
        }
        catch (NoSuchStoreException)
        {
            return Results.NotFound();
        }
        catch (BrightstarClientException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<string?> ReadUpdateTextAsync(HttpRequest request)
    {
        if (request.ContentType != null && request.ContentType.StartsWith(SparqlUpdateMediaType, StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(request.Body);
            return await reader.ReadToEndAsync(request.HttpContext.RequestAborted);
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(request.HttpContext.RequestAborted);
            return form["update"].FirstOrDefault();
        }

        return null;
    }
}
