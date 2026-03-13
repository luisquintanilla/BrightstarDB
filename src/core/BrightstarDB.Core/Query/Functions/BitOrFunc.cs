using System;
using VDS.RDF;
using VDS.RDF.Nodes;
using VDS.RDF.Query;
using VDS.RDF.Query.Expressions;

namespace BrightstarDB.Query.Functions
{
    internal class BitOrFunc : BaseBinaryExpression
    {
        public BitOrFunc(ISparqlExpression arg1, ISparqlExpression arg2) : base(arg1, arg2)
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
                    return (TResult)(object)new LongNode(aNode.AsInteger() | bNode.AsInteger());
                }
            }
            throw new RdfQueryException("Cannot evaluate bitwise OR expression as the arguments are not integer values.");
        }

        public override T Accept<T>(ISparqlExpressionVisitor<T> visitor)
        {
            return default;
        }

        public override ISparqlExpression Transform(IExpressionTransformer transformer)
        {
            return new BitOrFunc(transformer.Transform(_leftExpr), transformer.Transform(_rightExpr));
        }

        public override SparqlExpressionType Type => SparqlExpressionType.Function;

        public override string Functor => BrightstarFunctionFactory.BrightstarFunctionsNamespace + BrightstarFunctionFactory.BitOr;

        public override string ToString()
        {
            return "<" + Functor + ">(" + _leftExpr + ", " + _rightExpr + ")";
        }
    }
}