#nullable enable
using System;
using System.Collections.Generic;
using BrightstarDB;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Algebra;
using VDS.RDF.Writing;
using StringWriter = System.IO.StringWriter;

namespace BrightstarDB.Server.AspNetCore.Models;

public class GraphListModel
{
    public const string SparqlResultVariableName = "graphUri";

    public List<string> Graphs { get; private set; }

    public GraphListModel(IEnumerable<string> graphList)
    {
        Graphs = new List<string>(graphList);
    }

    public string AsString(SparqlResultsFormat format)
    {
        var g = new VDS.RDF.Graph();
        var results = new List<SparqlResult>();
        foreach (var graphUri in Graphs)
        {
            var bindings = new[]
            {
                new KeyValuePair<string, INode>(SparqlResultVariableName, g.CreateUriNode(new Uri(graphUri)))
            };
            results.Add(new SparqlResult(bindings));
        }
        var rs = new SparqlResultSet(results);
        var writer = GetWriter(format);
        var sw = new StringWriter();
        writer.Save(rs, sw);
        sw.Flush();
        return sw.ToString();
    }

    private static ISparqlResultsWriter GetWriter(SparqlResultsFormat format)
    {
        return format switch
        {
            _ when format == SparqlResultsFormat.Csv => new SparqlCsvWriter(),
            _ when format == SparqlResultsFormat.Tsv => new SparqlTsvWriter(),
            _ when format == SparqlResultsFormat.Json => new SparqlJsonWriter(),
            _ => new SparqlXmlWriter(),
        };
    }
}
