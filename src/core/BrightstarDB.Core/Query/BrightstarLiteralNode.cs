using System;
using BrightstarDB.Rdf;

namespace BrightstarDB.Query
{
#if !SILVERLIGHT && !PORTABLE && !NETCORE
    [Serializable]
#endif
    internal class BrightstarLiteralNode : VDS.RDF.LiteralNode
    {
        private BrightstarLiteralNode(string value, string langCode)  : base(value, langCode, false)
        {            
        }

        private BrightstarLiteralNode(string value, Uri datatype) : base(value, datatype, false){}

        /// <summary>
        /// Create a literal node. For plain literals (no language, PlainLiteral or null datatype),
        /// normalizes to xsd:string for RDF 1.1 compatibility with dotNetRDF 3.x.
        /// </summary>
        public static BrightstarLiteralNode Create(string value, string datatype, string languageCode)
        {
            if (!string.IsNullOrEmpty(languageCode))
            {
                return new BrightstarLiteralNode(value, languageCode);
            }
            if (datatype == null || datatype.Equals(RdfDatatypes.PlainLiteral))
            {
                // In RDF 1.1, plain literals are xsd:string
                return new BrightstarLiteralNode(value, new Uri(RdfDatatypes.String));
            }
            return new BrightstarLiteralNode(value, new Uri(datatype));
        }
    }
}