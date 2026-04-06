#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BrightstarDB.Client;
using Microsoft.AspNetCore.Http;

namespace BrightstarDB.Server.AspNetCore.Formatters;

public sealed class SparqlQueryResult(
    IBrightstarService brightstarService,
    string storeName,
    ulong? commitId,
    string query,
    IReadOnlyList<string>? defaultGraphUris,
    SparqlResultsFormat? resultsFormat,
    RdfFormat? graphFormat) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            Stream resultStream;
            ISerializationFormat streamFormat;
            if (commitId.HasValue)
            {
                var commitPoint = brightstarService.GetCommitPoint(storeName, commitId.Value);
                if (commitPoint == null)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                resultStream = brightstarService.ExecuteQuery(
                    commitPoint,
                    query,
                    defaultGraphUris,
                    resultsFormat,
                    graphFormat,
                    out streamFormat);
            }
            else
            {
                var ifModifiedSince = httpContext.Request.GetTypedHeaders().IfModifiedSince?.UtcDateTime;
                resultStream = brightstarService.ExecuteQuery(
                    storeName,
                    query,
                    defaultGraphUris,
                    ifModifiedSince,
                    resultsFormat,
                    graphFormat,
                    out streamFormat);
            }

            await using (resultStream.ConfigureAwait(false))
            {
                httpContext.Response.StatusCode = StatusCodes.Status200OK;
                httpContext.Response.ContentType = streamFormat.ToString();
                await resultStream.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
            }
        }
        catch (BrightstarStoreNotModifiedException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status304NotModified;
        }
        catch (NoSuchStoreException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        catch (BrightstarClientException ex)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsync(ex.Message, httpContext.RequestAborted);
        }
    }
}
