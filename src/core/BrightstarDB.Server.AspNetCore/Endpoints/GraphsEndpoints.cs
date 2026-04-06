#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BrightstarDB;
using BrightstarDB.Client;
using BrightstarDB.Server.AspNetCore.Authorization;
using BrightstarDB.Server.AspNetCore.Formatters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Handlers;
using StringWriter = System.IO.StringWriter;

namespace BrightstarDB.Server.AspNetCore.Endpoints;

public static class GraphsEndpoints
{
    private const string DefaultGraphContentQuery = "CONSTRUCT { ?s ?p ?o } WHERE { ?s ?p ?o }";
    private const string NamedGraphContentQuery = "CONSTRUCT {{ ?s ?p ?o }} WHERE {{ GRAPH <{0}> {{ ?s ?p ?o }} }}";
    private const string UpdateNamedGraph = "DROP SILENT GRAPH <{0}>; INSERT DATA {{ GRAPH <{0}> {{ {1} }} }}";
    private const string UpdateDefaultGraph = "DROP SILENT DEFAULT; INSERT DATA {{ {0} }}";
    private const string DropDefaultGraph = "DROP DEFAULT";
    private const string DropNamedGraph = "DROP GRAPH <{0}>";
    private const string MergeNamedGraph = "INSERT DATA {{ GRAPH <{0}> {{ {1} }} }}";
    private const string MergeDefaultGraph = "INSERT DATA {{ {0} }}";

    public static IEndpointRouteBuilder MapGraphsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{storeName}/graphs", HandleGetGraphs)
            .WithName("GetGraphs")
            .WithTags("Graphs")
            .WithSummary("List or retrieve graphs");
        endpoints.MapPut("/{storeName}/graphs", HandlePutGraph)
            .WithName("PutGraph")
            .WithTags("Graphs")
            .WithSummary("Replace a graph");
        endpoints.MapPost("/{storeName}/graphs", HandlePostGraph)
            .WithName("PostGraph")
            .WithTags("Graphs")
            .WithSummary("Merge data into a graph");
        endpoints.MapDelete("/{storeName}/graphs", HandleDeleteGraph)
            .WithName("DeleteGraph")
            .WithTags("Graphs")
            .WithSummary("Delete a graph");
        return endpoints;
    }

    private static IResult HandleGetGraphs(
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

        var graphRequest = TryGetGraphUri(httpContext.Request, out var graphUri);
        if (graphRequest == GraphRequestState.Invalid) return TypedResults.BadRequest();

        if (graphRequest == GraphRequestState.None)
        {
            var graphs = brightstarService.ListNamedGraphs(storeName).ToArray();
            if (SparqlResultFormatHelper.ShouldWriteGraphListAsSparqlResults(httpContext.Request))
            {
                var selection = SparqlResultFormatHelper.Resolve(httpContext.Request, SerializableModel.SparqlResultSet);
                return new GraphListResult(graphs, selection.ResultsFormat);
            }

            return TypedResults.Json(graphs);
        }

        var sparqlQuery = graphUri == Constants.DefaultGraphUri
            ? DefaultGraphContentQuery
            : string.Format(NamedGraphContentQuery, graphUri);
        var selectionForGraph = SparqlResultFormatHelper.Resolve(httpContext.Request, SerializableModel.RdfGraph);
        return new SparqlQueryResult(
            brightstarService,
            storeName,
            null,
            sparqlQuery,
            [Constants.DefaultGraphUri],
            selectionForGraph.ResultsFormat,
            selectionForGraph.GraphFormat);
    }

    private static async Task<IResult> HandlePutGraph(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        return await WriteGraphAsync(storeName, httpContext, brightstarService, permissionsProvider, replaceGraph: true);
    }

    private static async Task<IResult> HandlePostGraph(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        return await WriteGraphAsync(storeName, httpContext, brightstarService, permissionsProvider, replaceGraph: false);
    }

    private static IResult HandleDeleteGraph(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.TransactionUpdate))
        {
            return TypedResults.Unauthorized();
        }

        if (!brightstarService.DoesStoreExist(storeName)) return TypedResults.NotFound();
        var graphRequest = TryGetGraphUri(httpContext.Request, out var graphUri);
        if (graphRequest != GraphRequestState.Targeted || graphUri == null) return TypedResults.BadRequest();

        string sparqlUpdate;
        string jobName;
        if (graphUri == Constants.DefaultGraphUri)
        {
            sparqlUpdate = DropDefaultGraph;
            jobName = "Drop Default Graph";
        }
        else
        {
            var namedGraphs = brightstarService.ListNamedGraphs(storeName).ToArray();
            if (!namedGraphs.Contains(graphUri, StringComparer.Ordinal)) return TypedResults.NotFound();
            sparqlUpdate = string.Format(DropNamedGraph, graphUri);
            jobName = "Drop Graph " + graphUri;
        }

        try
        {
            var job = brightstarService.ExecuteUpdate(storeName, sparqlUpdate, true, jobName);
            return job.JobCompletedOk ? TypedResults.NoContent() : TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (NoSuchStoreException)
        {
            return TypedResults.NotFound();
        }
        catch (BrightstarClientException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> WriteGraphAsync(
        string storeName,
        HttpContext httpContext,
        IBrightstarService brightstarService,
        AbstractStorePermissionsProvider permissionsProvider,
        bool replaceGraph)
    {
        if (!permissionsProvider.HasStorePermission(httpContext.User, storeName, StorePermissions.TransactionUpdate))
        {
            return TypedResults.Unauthorized();
        }

        if (!brightstarService.DoesStoreExist(storeName)) return TypedResults.NotFound();
        var graphRequest = TryGetGraphUri(httpContext.Request, out var graphUri);
        if (graphRequest != GraphRequestState.Targeted || graphUri == null) return TypedResults.BadRequest();

        var namedGraphs = graphUri == Constants.DefaultGraphUri ? Array.Empty<string>() : brightstarService.ListNamedGraphs(storeName).ToArray();
        var isNewGraph = graphUri != Constants.DefaultGraphUri && !namedGraphs.Contains(graphUri, StringComparer.Ordinal);

        try
        {
            var rdfFormat = GetRequestBodyFormat(httpContext.Request);
            if (rdfFormat == null) return TypedResults.StatusCode(StatusCodes.Status406NotAcceptable);

            var rdfPayload = await ParseBodyAsync(httpContext.Request, rdfFormat);
            var sparqlUpdate = graphUri == Constants.DefaultGraphUri
                ? string.Format(replaceGraph ? UpdateDefaultGraph : MergeDefaultGraph, rdfPayload)
                : string.Format(replaceGraph ? UpdateNamedGraph : MergeNamedGraph, graphUri, rdfPayload);

            var job = brightstarService.ExecuteUpdate(storeName, sparqlUpdate, true, "Update Graph " + graphUri);
            if (!job.JobCompletedOk) return TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError);
            return isNewGraph ? TypedResults.StatusCode(StatusCodes.Status201Created) : TypedResults.NoContent();
        }
        catch (RdfException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
        catch (NoSuchStoreException)
        {
            return TypedResults.NotFound();
        }
        catch (BrightstarClientException ex)
        {
            return TypedResults.BadRequest(new { error = ex.Message });
        }
    }

    private static GraphRequestState TryGetGraphUri(HttpRequest request, out string? graphUri)
    {
        if (request.Query.ContainsKey("default"))
        {
            graphUri = Constants.DefaultGraphUri;
            return GraphRequestState.Targeted;
        }

        if (!request.Query.ContainsKey("graph"))
        {
            graphUri = null;
            return GraphRequestState.None;
        }

        var graphValue = request.Query["graph"].FirstOrDefault();
        if (Uri.TryCreate(graphValue, UriKind.Absolute, out var graph))
        {
            graphUri = graph.ToString();
            return GraphRequestState.Targeted;
        }

        graphUri = null;
        return GraphRequestState.Invalid;
    }

    private static Task<string> ParseBodyAsync(HttpRequest request, RdfFormat contentType)
    {
        var parser = MimeTypesHelper.GetParser(contentType.MediaTypes.First());
        var writer = new WriteToStringHandler(typeof(VDS.RDF.Writing.Formatting.NTriplesFormatter));
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024, leaveOpen: true);
        parser.Load(writer, reader);
        return Task.FromResult(writer.ToString());
    }

    private static RdfFormat? GetRequestBodyFormat(HttpRequest request)
    {
        return request.ContentType == null ? RdfFormat.RdfXml : RdfFormat.GetResultsFormat(request.ContentType);
    }

    private enum GraphRequestState
    {
        None,
        Targeted,
        Invalid
    }

    private sealed class WriteToStringHandler : BaseRdfHandler
    {
        private readonly Type _formatterType;
        private readonly StringBuilder _buffer = new();
        private WriteThroughHandler? _handler;

        public WriteToStringHandler(Type formatterType)
        {
            _formatterType = formatterType;
        }

        protected override void StartRdfInternal()
        {
            _handler = new WriteThroughHandler(_formatterType, new StringWriter(_buffer));
            _handler.StartRdf();
        }

        protected override void EndRdfInternal(bool ok)
        {
            _handler?.EndRdf(ok);
        }

        protected override bool HandleBaseUriInternal(Uri baseUri)
        {
            return _handler?.HandleBaseUri(baseUri) ?? true;
        }

        protected override bool HandleNamespaceInternal(string prefix, Uri namespaceUri)
        {
            return _handler?.HandleNamespace(prefix, namespaceUri) ?? true;
        }

        protected override bool HandleTripleInternal(Triple t)
        {
            return _handler?.HandleTriple(t) ?? true;
        }

        protected override bool HandleQuadInternal(Triple t, IRefNode graph)
        {
            return _handler?.HandleTriple(t) ?? true;
        }

        public override bool AcceptsAll => true;

        public override string ToString()
        {
            return _buffer.ToString();
        }
    }
}
