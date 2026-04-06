#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BrightstarDB.Server.AspNetCore.Models;
using Microsoft.AspNetCore.Http;

namespace BrightstarDB.Server.AspNetCore.Formatters;

public sealed class GraphListResult : IResult
{
    private readonly IReadOnlyList<string> _graphs;
    private readonly SparqlResultsFormat? _resultsFormat;

    public GraphListResult(IEnumerable<string> graphs, SparqlResultsFormat? resultsFormat)
    {
        _graphs = graphs.ToArray();
        _resultsFormat = resultsFormat;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        if (_resultsFormat == null)
        {
            await TypedResults.Json(_graphs).ExecuteAsync(httpContext);
            return;
        }

        var body = new GraphListModel(_graphs).AsString(_resultsFormat);
        httpContext.Response.StatusCode = StatusCodes.Status200OK;
        httpContext.Response.ContentType = _resultsFormat.ToString();
        await httpContext.Response.WriteAsync(body, _resultsFormat.Encoding, httpContext.RequestAborted);
    }
}
