using System;
using System.Collections.Generic;
using System.IO;

#if !SILVERLIGHT && !PORTABLE
using System.IO.Compression;
#endif

using BrightstarDB.Query;
using VDS.RDF;

namespace BrightstarDB.Rdf
{
    internal class BrightstarRdfParserAdapter : IRdfHandler, IRdfParser
    {
        private readonly IRdfReader _rdfReader;
        private ITripleSink _sink;
        private string _defaultGraphUri;
        private long _bnodeCount;
        private readonly bool _decompressStream;

        public BrightstarRdfParserAdapter(IRdfReader rdfReader, bool decompressStream)
        {
            _rdfReader = rdfReader;
            _decompressStream = decompressStream;
#if SILVERLIGHT
            if (_decompressStream == true) throw new BrightstarInternalException("Compression not supported on Mobile");
#endif
        }

        #region Implementation of INodeFactory

        public IBlankNode CreateBlankNode()
        {
            return new BrightstarBlankNode(GetNextBlankNodeID());
        }

        public IBlankNode CreateBlankNode(string nodeId)
        {
            return new BrightstarBlankNode(nodeId);
        }

        public IGraphLiteralNode CreateGraphLiteralNode()
        {
            throw new NotImplementedException();
        }

        public IGraphLiteralNode CreateGraphLiteralNode(IGraph subgraph)
        {
            throw new NotImplementedException();
        }

        public ILiteralNode CreateLiteralNode(string literal, Uri datatype)
        {
            return BrightstarLiteralNode.Create(literal, datatype == null ? null : datatype.ToString(), null);
        }

        public ILiteralNode CreateLiteralNode(string literal)
        {
            return BrightstarLiteralNode.Create(literal, null, null);
        }

        public ILiteralNode CreateLiteralNode(string literal, string langspec)
        {
            return BrightstarLiteralNode.Create(literal, null, langspec);
        }

        public IUriNode CreateUriNode(Uri uri)
        {
            return new BrightstarUriNode(uri);
        }

        public IVariableNode CreateVariableNode(string varname)
        {
            throw new NotImplementedException();
        }

        public string GetNextBlankNodeID()
        {
            return (_bnodeCount++).ToString();
        }

        // dotNetRDF 3.x INodeFactory additions
        public Uri BaseUri { get; set; }

        public INamespaceMapper NamespaceMap { get; } = new NamespaceMapper();

        public IUriFactory UriFactory { get; set; } = new CachingUriFactory(null);

        public bool NormalizeLiteralValues { get; set; }

        public LanguageTagValidationMode LanguageTagValidation { get; set; } = LanguageTagValidationMode.None;

        public ITripleNode CreateTripleNode(Triple triple)
        {
            throw new NotSupportedException("RDF-star triple nodes are not supported by BrightstarDB.");
        }

        public IUriNode CreateUriNode(string qName)
        {
            throw new NotSupportedException("QName-based URI node creation is not supported by BrightstarDB.");
        }

        public IUriNode CreateUriNode()
        {
            throw new NotSupportedException("No-argument URI node creation is not supported by BrightstarDB.");
        }

        public Uri ResolveQName(string qName)
        {
            throw new NotSupportedException("QName resolution is not supported by BrightstarDB.");
        }

        #endregion

        #region Implementation of IRdfHandler

        public void StartRdf()
        {
            // Nothing to do
        }

        public void EndRdf(bool ok)
        {
            // Nothing to do
        }

        public bool HandleNamespace(string prefix, Uri namespaceUri)
        {
            // Not sure if we need to do anything here
            return true;
        }

        public bool HandleBaseUri(Uri baseUri)
        {
            // Again do we need to do anything ?
            return true;
        }

        public bool HandleTriple(Triple t)
        {
            return HandleTripleWithGraph(t, _defaultGraphUri);
        }

        public bool HandleQuad(Triple t, IRefNode graph)
        {
            string graphUri;
            if (graph is IUriNode uriNode)
            {
                graphUri = uriNode.Uri.ToString();
            }
            else
            {
                graphUri = _defaultGraphUri;
            }
            return HandleTripleWithGraph(t, graphUri);
        }

        private bool HandleTripleWithGraph(Triple t, string graphUri)
        {
            // Pass the triple through to the B* triple sink
            string subject = t.Subject.ToString();
            bool subjectIsBNode = t.Subject is IBlankNode;
            string predicate = t.Predicate.ToString();
            bool predicateIsBNode = t.Predicate is IBlankNode;

            if (t.Object is IBlankNode)
            {
                _sink.Triple(subject, subjectIsBNode, predicate, predicateIsBNode, t.Object.ToString(), true, false,
                             null, null, graphUri);
            }
            else if (t.Object is IUriNode)
            {
                _sink.Triple(subject, subjectIsBNode, predicate, predicateIsBNode, t.Object.ToString(), false, false,
                             null, null, graphUri);
            }
            else
            {
                var literal = t.Object as ILiteralNode;
                if (literal != null)
                {
                    _sink.Triple(subject, subjectIsBNode, predicate, predicateIsBNode,
                                 literal.Value, false, true,
                                 literal.DataType == null ? Constants.DefaultDatatypeUri : literal.DataType.ToString(),
                                 literal.Language, graphUri);
                }
                else
                {
                    throw new BrightstarInternalException(
                        String.Format("Unexpected object node type {0} in input stream.", t.Object.GetType()));
                }
            }
            return true;
        }

        public bool AcceptsAll
        {
            get { return true; }
        }

        #endregion

        #region Implementation of IRdfParser

        /// <summary>
        /// Parse the contents of <paramref name="data"/> as an RDF data stream
        /// </summary>
        /// <param name="data">The data stream to parse</param>
        /// <param name="sink">The target for the parsed RDF statements</param>
        /// <param name="defaultGraphUri">The default graph URI to assign to each of the parsed statements</param>
        public void Parse(Stream data, ITripleSink sink, string defaultGraphUri)
        {
            _sink = sink;
            _defaultGraphUri = defaultGraphUri;
            using (
#if !SILVERLIGHT && !PORTABLE
                var streamReader = _decompressStream
                                       ? new StreamReader(new GZipStream(data, CompressionMode.Decompress))
                                       : new StreamReader(data))
#else
                var streamReader = new StreamReader(data))
#endif
            {
                _rdfReader.Load(this, streamReader);
            }
        }

        /// <summary>
        /// Parse from a text reader
        /// </summary>
        /// <param name="reader">The reader providing the data to be parsed</param>
        /// <param name="sink">The target for the parsed RDF statements</param>
        /// <param name="defaultGraphUri">The default graph URI to assign to each of the parsed statements</param>
        public void Parse(TextReader reader, ITripleSink sink, string defaultGraphUri)
        {
            _sink = sink;
            _defaultGraphUri = defaultGraphUri;
            _rdfReader.Load(this, reader);
        }

        #endregion
    }
}
