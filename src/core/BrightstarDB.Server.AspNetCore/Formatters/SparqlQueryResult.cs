#nullable enable

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using BrightstarDB.Client;
using Microsoft.AspNetCore.Http;

namespace BrightstarDB.Server.AspNetCore.Formatters;

public sealed class SparqlQueryResult : IResult
{
    private readonly IBrightstarService _brightstarService;
    private readonly string _storeName;
    private readonly ulong? _commitId;
    private readonly string _query;
    private readonly IReadOnlyList<string>? _defaultGraphUris;
    private readonly SparqlResultsFormat? _resultsFormat;
    private readonly RdfFormat? _graphFormat;

    public SparqlQueryResult(
        IBrightstarService brightstarService,
        string storeName,
        ulong? commitId,
        string query,
        IReadOnlyList<string>? defaultGraphUris,
        SparqlResultsFormat? resultsFormat,
        RdfFormat? graphFormat)
    {
        _brightstarService = brightstarService;
        _storeName = storeName;
        _commitId = commitId;
        _query = query;
        _defaultGraphUris = defaultGraphUris;
        _resultsFormat = resultsFormat;
        _graphFormat = graphFormat;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        try
        {
            Stream resultStream;
            ISerializationFormat streamFormat;
            if (_commitId.HasValue)
            {
                var commitPoint = _brightstarService.GetCommitPoint(_storeName, _commitId.Value);
                if (commitPoint == null)
                {
                    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                resultStream = _brightstarService.ExecuteQuery(
                    commitPoint,
                    _query,
                    _defaultGraphUris,
                    _resultsFormat,
                    _graphFormat,
                    out streamFormat);
            }
            else
            {
                var ifModifiedSince = httpContext.Request.GetTypedHeaders().IfModifiedSince?.UtcDateTime;
                resultStream = _brightstarService.ExecuteQuery(
                    _storeName,
                    _query,
                    _defaultGraphUris,
                    ifModifiedSince,
                    _resultsFormat,
                    _graphFormat,
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
