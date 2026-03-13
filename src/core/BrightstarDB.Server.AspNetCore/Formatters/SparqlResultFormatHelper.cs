#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace BrightstarDB.Server.AspNetCore.Formatters;

public sealed record SparqlFormatSelection(SparqlResultsFormat? ResultsFormat, RdfFormat? GraphFormat)
{
    public ISerializationFormat SerializationFormat => ResultsFormat is not null
        ? ResultsFormat
        : GraphFormat is not null
            ? GraphFormat
            : SparqlResultsFormat.Xml;

    public string ContentType => SerializationFormat.ToString() ?? "application/sparql-results+xml; charset=utf-8";
}

public static class SparqlResultFormatHelper
{
    private const string JsonLdMediaType = "application/ld+json";

    public static SparqlFormatSelection Resolve(HttpRequest request, SerializableModel resultModel, IEnumerable<string>? formatOverrides = null)
    {
        if (TryResolveOverride(formatOverrides, resultModel, out var overrideSelection))
        {
            return overrideSelection;
        }

        if (resultModel == SerializableModel.RdfGraph)
        {
            if (TryGetGraphFormat(request.GetTypedHeaders().Accept, out var graphFormat))
            {
                return new SparqlFormatSelection(null, graphFormat);
            }

            return new SparqlFormatSelection(null, RdfFormat.RdfXml);
        }

        if (TryGetResultsFormat(request.GetTypedHeaders().Accept, out var resultsFormat))
        {
            return new SparqlFormatSelection(resultsFormat, null);
        }

        return new SparqlFormatSelection(SparqlResultsFormat.Xml, null);
    }

    public static bool ShouldWriteGraphListAsSparqlResults(HttpRequest request, IEnumerable<string>? formatOverrides = null)
    {
        return TryResolveOverride(formatOverrides, SerializableModel.SparqlResultSet, out _) ||
               TryGetResultsFormat(request.GetTypedHeaders().Accept, out _);
    }

    public static bool TryResolveOverride(IEnumerable<string>? formatOverrides, SerializableModel resultModel, out SparqlFormatSelection selection)
    {
        if (formatOverrides != null)
        {
            foreach (var formatValue in formatOverrides)
            {
                if (resultModel == SerializableModel.RdfGraph)
                {
                    var graphFormat = TryMapGraphFormat(formatValue);
                    if (graphFormat != null)
                    {
                        selection = new SparqlFormatSelection(null, graphFormat);
                        return true;
                    }
                }
                else
                {
                    var resultsFormat = TryMapResultsFormat(formatValue);
                    if (resultsFormat != null)
                    {
                        selection = new SparqlFormatSelection(resultsFormat, null);
                        return true;
                    }
                }
            }
        }

        selection = new SparqlFormatSelection(
            resultModel == SerializableModel.RdfGraph ? null : SparqlResultsFormat.Xml,
            resultModel == SerializableModel.RdfGraph ? RdfFormat.RdfXml : null);
        return false;
    }

    public static IEnumerable<string> ReadValues(IQueryCollection query, string key)
    {
        return SplitValues(query[key]);
    }

    public static IEnumerable<string> ReadValues(IFormCollection form, string key)
    {
        return SplitValues(form[key]);
    }

    private static IEnumerable<string> SplitValues(StringValues values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            foreach (var splitValue in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(splitValue)) yield return splitValue;
            }
        }
    }

    private static bool TryGetResultsFormat(IList<MediaTypeHeaderValue>? accepts, out SparqlResultsFormat? resultsFormat)
    {
        foreach (var accept in OrderAccepts(accepts))
        {
            resultsFormat = TryMapResultsFormat(accept.MediaType.Value);
            if (resultsFormat != null) return true;
        }

        resultsFormat = null;
        return false;
    }

    private static bool TryGetGraphFormat(IList<MediaTypeHeaderValue>? accepts, out RdfFormat? graphFormat)
    {
        foreach (var accept in OrderAccepts(accepts))
        {
            graphFormat = TryMapGraphFormat(accept.MediaType.Value);
            if (graphFormat != null) return true;
        }

        graphFormat = null;
        return false;
    }

    private static IEnumerable<MediaTypeHeaderValue> OrderAccepts(IList<MediaTypeHeaderValue>? accepts)
    {
        return accepts == null
            ? Enumerable.Empty<MediaTypeHeaderValue>()
            : accepts.OrderByDescending(x => x.Quality ?? 1.0d);
    }

    private static SparqlResultsFormat? TryMapResultsFormat(string? mediaTypeOrExtension)
    {
        return string.IsNullOrWhiteSpace(mediaTypeOrExtension)
            ? null
            : SparqlResultsFormat.GetResultsFormat(mediaTypeOrExtension);
    }

    private static RdfFormat? TryMapGraphFormat(string? mediaTypeOrExtension)
    {
        if (string.IsNullOrWhiteSpace(mediaTypeOrExtension)) return null;
        if (mediaTypeOrExtension.Equals(JsonLdMediaType, StringComparison.OrdinalIgnoreCase))
        {
            return RdfFormat.Json;
        }

        return RdfFormat.GetResultsFormat(mediaTypeOrExtension);
    }
}
