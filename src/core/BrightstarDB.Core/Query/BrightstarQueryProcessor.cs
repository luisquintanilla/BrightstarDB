using System;
using BrightstarDB.Query.Processor;
using BrightstarDB.Storage;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Optimisation;

namespace BrightstarDB.Query
{
    internal class BrightstarQueryProcessor : LeviathanQueryProcessor
    {
        private static readonly Action<LeviathanQueryOptions> ConfigureOptions = options =>
        {
            var optimiser = new SparqlOptimiser();
            optimiser.AddOptimiser(new VariableEqualsOptimizer());
            optimiser.AddOptimiser(new JoinOptimiser());
            foreach (var opt in optimiser.AlgebraOptimisers)
            {
                options.AlgebraOptimisers = options.AlgebraOptimisers is null
                    ? new[] { opt }
                    : new System.Collections.Generic.List<IAlgebraOptimiser>(options.AlgebraOptimisers) { opt };
            }
        };

        public BrightstarQueryProcessor(IInMemoryQueryableStore store) : base(store, ConfigureOptions)
        {
        }

        public BrightstarQueryProcessor(IStore store, ISparqlDataset data) : base(data, ConfigureOptions)
        {
        }
    }
}