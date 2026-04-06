using System;
using Remotion.Linq.Clauses;

namespace BrightstarDB.EntityFramework.Query
{
    /// <summary>
    /// Represents a single clause in the ORDER BY part of a SPARQL Query
    /// </summary>
    public class SparqlOrdering(string selectorExpression, OrderingDirection orderingDirection)
    {
        /// <summary>
        /// Get the expression used for sorting
        /// </summary>
        public string SelectorExpression { get; } = selectorExpression;

        /// <summary>
        /// Get the direction of the sort
        /// </summary>
        public OrderingDirection OrderingDirection { get; } = orderingDirection;

        /// <summary>
        /// Returns the SPARQL substring for the ordering clause
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format(
                OrderingDirection == OrderingDirection.Asc ? "ASC({0})" : "DESC({0})",
                SelectorExpression);
        }
    }
}