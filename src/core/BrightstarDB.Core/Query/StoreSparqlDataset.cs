using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Rdf;
using BrightstarDB.Storage;
using VDS.RDF;
using VDS.RDF.Query.Datasets;

namespace BrightstarDB.Query
{
    internal class StoreSparqlDataset : ISparqlDataset
    {
        /// <summary>
        /// The store againts which we are running the query
        /// </summary>
        private readonly IStore _store;

        private List<string> _graphUris = new List<string> { Constants.DefaultGraphUri };
        private List<string> _defaultGraphUris = new List<string> { Constants.DefaultGraphUri };

        public StoreSparqlDataset(IStore store)
        {
            _store = store;
        }

        #region Helper

        private static Uri GetUri(IRefNode refNode)
        {
            return refNode is IUriNode un ? un.Uri : null;
        }

        #endregion

        #region Active/Default Graph — Uri overloads (existing)

        public void SetActiveGraph(IEnumerable<Uri> graphUris)
        {
            _graphUris = graphUris.Select(g => g.ToString()).ToList();
        }

        public void SetActiveGraph(Uri graphUri)
        {
            _graphUris.Clear();
            _graphUris.Add(graphUri.ToString());
        }

        public void SetDefaultGraph(Uri graphUri)
        {
            _defaultGraphUris.Clear();
            _defaultGraphUris.Add(graphUri.ToString());
        }

        public void SetDefaultGraph(IEnumerable<Uri> graphUris)
        {
            _defaultGraphUris = graphUris.Select(g => g.ToString()).ToList();
        }

        public void SetActiveGraph(IGraph g)
        {
            _graphUris.Clear();
            var graphUri = g == null ? Constants.DefaultGraphUri : g.BaseUri.ToString();
            _graphUris.Add(graphUri);
        }

        public void SetDefaultGraph(IGraph g)
        {
            //_defaultGraphUri = g == null ? Constants.DefaultGraphUri : g.BaseUri.ToString();
            if (g == null)
            {
                SetDefaultGraph(new Uri(Constants.DefaultGraphUri));
            }
            else
            {
                SetDefaultGraph(g.BaseUri);
            }
        }

        #endregion

        #region Active/Default Graph — IRefNode overloads (dotNetRDF 3.x)

        public void SetActiveGraph(IRefNode graphName)
        {
            SetActiveGraph(GetUri(graphName));
        }

        public void SetActiveGraph(IList<IRefNode> graphNames)
        {
            _graphUris = graphNames.Select(n => GetUri(n)?.ToString() ?? Constants.DefaultGraphUri).ToList();
        }

        public void SetDefaultGraph(IRefNode graphName)
        {
            SetDefaultGraph(GetUri(graphName));
        }

        public void SetDefaultGraph(IList<IRefNode> graphNames)
        {
            _defaultGraphUris = graphNames.Select(n => GetUri(n)?.ToString() ?? Constants.DefaultGraphUri).ToList();
        }

        #endregion

        public void ResetActiveGraph()
        {
            _graphUris.Clear();
            _graphUris.AddRange(_defaultGraphUris);
        }

        public void ResetDefaultGraph()
        {
            _defaultGraphUris.Clear();
        }

        public bool AddGraph(IGraph g)
        {
            // we don't support IGraph based operations
            throw new NotSupportedException();
        }

        public bool RemoveGraph(Uri graphUri)
        {
            // we don't support IGraph based operations
            throw new NotSupportedException();
        }

        public bool HasGraph(Uri graphUri)
        {
            return _store.GetGraphUris().Contains(graphUri.ToString());
        }

        public bool HasGraph(IRefNode graphName)
        {
            // In dotNetRDF 3.x (RDF 1.1), the unnamed default graph has Name=null.
            // BrightstarDB uses a named default graph (Constants.DefaultGraphUri) as its default.
            // Treat null (unnamed default graph) as equivalent to BrightstarDB's named default graph.
            if (graphName == null) return true;
            var uri = GetUri(graphName);
            return uri != null && HasGraph(uri);
        }

        public IGraph GetModifiableGraph(Uri graphUri)
        {
            // we don't support IGraph based operations
            throw new NotSupportedException();
        }

        public IGraph GetModifiableGraph(IRefNode graphName)
        {
            throw new NotSupportedException();
        }

        public bool RemoveGraph(IRefNode graphName)
        {
            throw new NotSupportedException();
        }

        public IGraph this[IRefNode graphName]
        {
            get { throw new NotSupportedException(); }
        }

        public bool ContainsTriple(Triple t)
        {
            var objLit = t.Object as LiteralNode;
            if (objLit != null)
            {
                return MatchLiteralObject(
                    GetNodeMatchString(t.Subject),
                    GetNodeMatchString(t.Predicate),
                    objLit).Any();
            }
            return
                _store.Match(GetNodeMatchString(t.Subject),
                             GetNodeMatchString(t.Predicate),
                             GetNodeMatchString(t.Object),
                             false, null, null, _graphUris)
                    .Any();
        }

        private static Triple MakeVdsTriple(Model.Triple triple)
        {
            if (triple.IsLiteral)
            {
                LiteralNode literalNode = BrightstarLiteralNode.Create(triple.Object, triple.DataType, triple.LangCode);
                return new Triple(MakeVdsNode(triple.Subject), MakeVdsNode(triple.Predicate), literalNode);
            }
            return new Triple(MakeVdsNode(triple.Subject), MakeVdsNode(triple.Predicate), MakeVdsNode(triple.Object));
        }

        private static INode MakeVdsNode(string identifier)
        {
            return new BrightstarUriNode(new Uri(identifier));

            //return identifier.StartsWith("_:")
            //           ? new BrightstarBlankNode(identifier.Substring(2))
            //           : new BrightstarUriNode(new Uri(identifier)) as INode;
        }

        public IEnumerable<Triple> GetTriplesWithSubject(INode subj)
        {
            // dotNetRDF doesn't filter out these cases
            if (subj.NodeType == NodeType.Literal)
            {
                // Can't be a match if there is a literal for subject or predicate
                return new Triple[0];
            }


            return _store.Match(GetNodeMatchString(subj), null, null, graphs: _graphUris).Select(MakeVdsTriple);
        }

        public IEnumerable<Triple> GetTriplesWithPredicate(INode pred)
        {
            // dotNetRDF doesn't filter out these cases
            if (pred.NodeType == NodeType.Literal)
            {
                // Can't be a match if there is a literal for subject or predicate
                return new Triple[0];
            }

            return _store.Match(null, GetNodeMatchString(pred), null, graphs: _graphUris).Select(MakeVdsTriple);
        }

        public IEnumerable<Triple> GetTriplesWithObject(INode obj)
        {
            var objLit = obj as LiteralNode;

            if (objLit != null)
            {
                return MatchLiteralObject(null, null, objLit).Select(MakeVdsTriple);
            }
            return _store.Match(null, null, GetNodeMatchString(obj), false, null, null, _graphUris).Select(MakeVdsTriple);
        }

        public IEnumerable<Triple> GetTriplesWithSubjectPredicate(INode subj, INode pred)
        {
            // dotNetRDF doesn't filter out these cases
            if (subj.NodeType == NodeType.Literal || pred.NodeType == NodeType.Literal)
            {
                // Can't be a match if there is a literal for subject or predicate
                return new Triple[0];
            }
            return _store.Match(GetNodeMatchString(subj),
                                GetNodeMatchString(pred),
                                null,
                                graphs: _graphUris)
                .Select(MakeVdsTriple);
        }

        public IEnumerable<Triple> GetTriplesWithSubjectObject(INode subj, INode obj)
        {
            // dotNetRDF doesn't filter out these cases
            if (subj.NodeType == NodeType.Literal)
            {
                // Can't be a match if there is a literal for subject or predicate
                return new Triple[0];
            }
            var objLit = obj as LiteralNode;
            if (objLit != null)
            {
                return MatchLiteralObject(GetNodeMatchString(subj), null, objLit).Select(MakeVdsTriple);
            }
            return
                _store.Match(GetNodeMatchString(subj),
                             null,
                             GetNodeMatchString(obj),
                             false, null, null,
                             _graphUris)
                    .Select(MakeVdsTriple);
        }

        public IEnumerable<Triple> GetTriplesWithPredicateObject(INode pred, INode obj)
        {
            // dotNetRDF doesn't filter out these cases
            if (pred.NodeType == NodeType.Literal)
            {
                // Can't be a match if there is a literal for subject or predicate
                return new Triple[0];
            }


            var objLit = obj as LiteralNode;

            if (objLit != null)
            {
                return MatchLiteralObject(null, GetNodeMatchString(pred), objLit).Select(MakeVdsTriple);
            }
            return _store.Match(null,
                             GetNodeMatchString(pred),
                             GetNodeMatchString(obj),
                             false, null, null,
                             _graphUris)
                    .Select(MakeVdsTriple);
        }

        private static string GetNodeMatchString(INode node)
        {
            switch (node.NodeType)
            {
                case NodeType.Uri:
                    return ((IUriNode)node).Uri.ToString();
                case NodeType.Literal:
                    return ((ILiteralNode)node).Value;
                case NodeType.Blank:
                    // return ((IBlankNode)node).InternalID;
                    var s = node.ToString();
                    return s;
                default:
                    throw new BrightstarInternalException(
                        String.Format("Cannot convert node of type {0} to a node match string", node.GetType()));
            }
        }

        /// <summary>
        /// Determines if a literal node is a plain string (no language tag) with either
        /// PlainLiteral or xsd:string datatype. In RDF 1.1 (dotNetRDF 3.x), plain string
        /// literals get xsd:string, but the store may contain PlainLiteral from older data.
        /// </summary>
        private static bool IsPlainStringLiteral(ILiteralNode lit)
        {
            if (!string.IsNullOrEmpty(lit.Language)) return false;
            var dt = lit.DataType?.ToString();
            return string.IsNullOrEmpty(dt)
                   || dt.Equals(RdfDatatypes.PlainLiteral, StringComparison.Ordinal)
                   || dt.Equals(RdfDatatypes.String, StringComparison.Ordinal);
        }

        /// <summary>
        /// Match a literal object against the store. Searches both xsd:string and
        /// PlainLiteral datatypes for plain string literals to handle data stored by
        /// either dotNetRDF 3.x (xsd:string) or BrightstarDB's NTriples parser (PlainLiteral).
        /// </summary>
        /// <remarks>
        /// For non-empty strings, Store.Match safely returns empty when FindResourceId
        /// returns NullUlong (the guard: oid==NullUlong and !IsNullOrEmpty(obj) → empty).
        /// So concatenating two searches is safe — at most one will match.
        /// For empty strings, Store.Match treats NullUlong as a wildcard (because
        /// IsNullOrEmpty("") is true, bypassing the guard). To avoid wildcard results,
        /// we only search xsd:string for empty strings.
        /// </remarks>
        private IEnumerable<Model.Triple> MatchLiteralObject(
            string subject, string predicate, ILiteralNode lit)
        {
            var datatype = lit.DataType?.AbsoluteUri;
            // Normalize: treat null or PlainLiteral as xsd:string for RDF 1.1 querying
            if (string.IsNullOrEmpty(datatype) || datatype == RdfDatatypes.PlainLiteral)
            {
                datatype = RdfDatatypes.String;
            }

            if (datatype == RdfDatatypes.String)
            {
                if (string.IsNullOrEmpty(lit.Value))
                {
                    // Empty strings: avoid Concat due to Store.Match wildcard behavior.
                    return _store.Match(subject, predicate, "", true, RdfDatatypes.String, lit.Language, _graphUris);
                }
                // Non-empty strings: safe to Concat — unmatched datatype returns empty, not wildcard.
                return _store.Match(subject, predicate, lit.Value, true, RdfDatatypes.String, lit.Language, _graphUris)
                    .Concat(_store.Match(subject, predicate, lit.Value, true, RdfDatatypes.PlainLiteral, lit.Language, _graphUris));
            }

            return _store.Match(subject, predicate, lit.Value ?? "", true, datatype, lit.Language, _graphUris);
        }

        #region ITripleIndex — Uri overloads (dotNetRDF 3.x)

        public IEnumerable<Triple> GetTriples(Uri uri)
        {
            var node = new UriNode(uri);
            return GetTriples(node);
        }

        public IEnumerable<Triple> GetTriples(INode n)
        {
            return GetTriplesWithSubject(n)
                .Union(GetTriplesWithPredicate(n))
                .Union(GetTriplesWithObject(n));
        }

        public IEnumerable<Triple> GetTriplesWithSubject(Uri u)
        {
            return GetTriplesWithSubject(new UriNode(u));
        }

        public IEnumerable<Triple> GetTriplesWithPredicate(Uri u)
        {
            return GetTriplesWithPredicate(new UriNode(u));
        }

        public IEnumerable<Triple> GetTriplesWithObject(Uri u)
        {
            return GetTriplesWithObject(new UriNode(u));
        }

        #endregion

        #region ITripleIndex — Quoted triples (RDF-star, not supported)

        public bool ContainsQuotedTriple(Triple t)
        {
            return false;
        }

        public IEnumerable<Triple> QuotedTriples => Enumerable.Empty<Triple>();

        public IEnumerable<Triple> GetQuoted(Uri uri)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuoted(INode n)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithSubject(INode n)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithSubject(Uri u)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithPredicate(INode n)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithPredicate(Uri u)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithObject(INode n)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithObject(Uri u)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithSubjectPredicate(INode subj, INode pred)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithSubjectObject(INode subj, INode obj)
        {
            return Enumerable.Empty<Triple>();
        }

        public IEnumerable<Triple> GetQuotedWithPredicateObject(INode pred, INode obj)
        {
            return Enumerable.Empty<Triple>();
        }

        #endregion

        #region Graph name collections (IRefNode-based, dotNetRDF 3.x)

        public IEnumerable<IRefNode> GraphNames
        {
            get { return _store.GetGraphUris().Where(x => !x.Equals(Constants.DefaultGraphUri)).Select(x => (IRefNode)new UriNode(new Uri(x))); }
        }

        public IEnumerable<IRefNode> DefaultGraphNames
        {
            get { return _defaultGraphUris.Select(u => (IRefNode)new UriNode(new Uri(u))); }
        }

        public IEnumerable<IRefNode> ActiveGraphNames
        {
            get { return _graphUris.Select(u => (IRefNode)new UriNode(new Uri(u))); }
        }

        #endregion

        public void Flush()
        {
            return;
        }

        public void Discard()
        {
            // Nothing to do in this implementation yet
        }

        public IEnumerable<Uri> DefaultGraphUris
        {
            get { return _defaultGraphUris.Select(s => new Uri(s)); }
        }

        public IEnumerable<string> GetActiveGraphUris()
        {
            return _graphUris;
        }

        public IEnumerable<Uri> ActiveGraphUris
        {
            get { return _graphUris.Select(s => new Uri(s)); }
        }

        public IGraph DefaultGraph
        {
            get { return null; }
        }

        public IGraph ActiveGraph
        {
            get { return null; }
        }

        public bool UsesUnionDefaultGraph
        {
            get { return false; }
        }

        public IEnumerable<IGraph> Graphs
        {
            get { return null; }
        }

        public IEnumerable<Uri> GraphUris
        {
            get
            {
                return _store.GetGraphUris().Where(x => !x.Equals(Constants.DefaultGraphUri)).Select(x => new Uri(x));
            }
        }

        public IGraph this[Uri graphUri]
        {
            get { throw new NotSupportedException(); }
        }

        public bool HasTriples
        {
            get { return true; }
        }

        public IEnumerable<Triple> Triples
        {
            get
            {
                return _store.Match(null, null, null, false, null, null, _graphUris).Select(MakeVdsTriple);
            }
        }
    }
}

