using System;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Query;
using VDS.RDF.Query.Expressions;

namespace BrightstarDB.Query.Functions
{
    internal class BitAndFunc : BaseBinaryExpression
    {
        public BitAndFunc(ISparqlExpression arg1, ISparqlExpression arg2) : base(arg1, arg2)
        {
        }

        public override TResult Accept<TResult, TContext, TBinding>(ISparqlExpressionProcessor<TResult, TContext, TBinding> processor, TContext context, TBinding binding)
        {
            var a = _leftExpr.Accept(processor, context, binding);
            var b = _rightExpr.Accept(processor, context, binding);
            if (a is IValuedNode aNode && b is IValuedNode bNode)
            {
                var type = (SparqlNumericType)Math.Max((int)aNode.NumericType, (int)bNode.NumericType);
                if (type == SparqlNumericType.Integer)
                {
                    return (TResult)(object)new LongNode(aNode.AsInteger() & bNode.AsInteger());
                }
            }
            throw new RdfQueryException("Cannot evaluate bitwise AND expression as the arguments are not integer values.");
        }

        public override T Accept<T>(ISparqlExpressionVisitor<T> visitor)
        {
            throw new NotSupportedException("Visitor-based traversal is not supported for BrightstarDB custom SPARQL functions. Use the processor-based Accept overload instead.");
        }

        public override ISparqlExpression Transform(IExpressionTransformer transformer)
        {
            return new BitAndFunc(transformer.Transform(_leftExpr), transformer.Transform(_rightExpr));
        }

        public override SparqlExpressionType Type => SparqlExpressionType.Function;

        public override string Functor => BrightstarFunctionFactory.BrightstarFunctionsNamespace + BrightstarFunctionFactory.BitAnd;

        public override string ToString()
        {
            return "<" + Functor + ">(" + _leftExpr + ", " + _rightExpr + ")";
        }
    }
}