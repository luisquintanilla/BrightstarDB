using System.Collections.Generic;
using System.IO;
using System.Threading;
using BrightstarDB.EntityFramework.Query;
using BrightstarDB.Model;
using BrightstarDB.Rdf;
using VDS.RDF;

#if PORTABLE
using VDS.RDF; // Pulls in the extension methods for Close() on streams
#endif

namespace BrightstarDB.Client
{
    internal class BrightstarRestUpdatableStore(IBrightstarService client, string storeName) : IUpdateableStore
    {
        public SparqlResult ExecuteQuery(SparqlQueryContext queryContext, IList<string> datasetGraphUris)
        {
            ISerializationFormat resultFormat;
            var resultStream = client.ExecuteQuery(storeName, queryContext.SparqlQuery, datasetGraphUris, null, queryContext.SparqlResultsFormat,
                queryContext.GraphResultsFormat, out resultFormat);
            return new SparqlResult(resultStream, resultFormat, queryContext);
        }

        public void ApplyTransaction(IEnumerable<ITriple> existencePreconditions, IEnumerable<ITriple> nonexistencePreconditions,
            IEnumerable<ITriple> deletePatterns, IEnumerable<ITriple> inserts, string updateGraphUri)
        {
            var existencePreconditionsData = SerializeTriples(existencePreconditions);
            var nonexistencePreconditionsData = SerializeTriples(nonexistencePreconditions);
            var deleteData = SerializeTriples(deletePatterns);
            var addData = SerializeTriples(inserts);

            PostTransaction(existencePreconditionsData, nonexistencePreconditionsData, deleteData, addData,
                            updateGraphUri);
        }

        private static string SerializeTriples(IEnumerable<ITriple> triples)
        {
            if (triples == null) return string.Empty;
            using (var writer = new StringWriter())
            {
                var sink = new BrightstarTripleSinkAdapter(new NQuadsWriter(writer));
                foreach (var t in triples) sink.Triple(t);
                writer.Close();
                return writer.ToString();
            }
        }

        public void Cleanup()
        {
            // Nothing to do
        }

        private void PostTransaction(string existencePreconditions, string nonexistencePreconditions, string patternsToDelete, string triplesToAdd, string defaultGraphUri)
        {
            var jobInfo = client.ExecuteTransaction(storeName,
                                                     new UpdateTransactionData
                                                     {
                                                         ExistencePreconditions = existencePreconditions,
                                                         NonexistencePreconditions = nonexistencePreconditions,
                                                         DeletePatterns = patternsToDelete,
                                                         InsertData = triplesToAdd,
                                                         DefaultGraphUri = defaultGraphUri
                                                     });

            while (!(jobInfo.JobCompletedOk || jobInfo.JobCompletedWithErrors))
            {
#if PORTABLE
    // Very rudimentary synchronous wait
                var ev = new ManualResetEvent(false);
                ev.WaitOne(200);
#else
                Thread.Sleep(20);
#endif
                jobInfo = client.GetJobInfo(storeName, jobInfo.JobId);
            }

            if (jobInfo.JobCompletedWithErrors)
            {
                // if (jobInfo.ExceptionInfo.Type == typeof(Server.PreconditionFailedException).FullName)
                if (jobInfo.ExceptionInfo != null && jobInfo.ExceptionInfo.Type == "BrightstarDB.Server.PreconditionFailedException")
                {
                    throw TransactionPreconditionsFailedException.FromExceptionDetail(jobInfo.ExceptionInfo);
                }
                throw new BrightstarClientException("Error processing update transaction. " + jobInfo.StatusMessage);
            }
        }
    }
}