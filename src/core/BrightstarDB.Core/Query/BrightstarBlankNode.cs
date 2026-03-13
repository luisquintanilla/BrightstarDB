using VDS.RDF;

namespace BrightstarDB.Query
{
    internal class BrightstarBlankNode : BlankNode
    {
        public BrightstarBlankNode(string bnodeId) : base(bnodeId){}

        public override string ToString()
        {
            return Constants.GeneratedUriPrefix + InternalID;
        }
    }
}