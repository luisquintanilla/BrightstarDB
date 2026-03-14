using System.IO;
using System.Linq;
using BrightstarDB.Client;
using NUnit.Framework;

namespace BrightstarDB.Tests.EntityFramework
{
    [TestFixture]
    public class DotNetRdfContextTests
    {
        [Test]
#if PORTABLE
        [Ignore("DotNetRDF PCL does not support loading files into store configuration")]
#endif
        public void TestInitializeWithStoreConfiguration()
        {
            var configFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, Configuration.DataLocation + "dataObjectStoreConfig.ttl");
            var connectionString = "type=dotNetRdf;configuration=" + configFilePath + ";storeName=http://www.brightstardb.com/tests#people";
            const string baseGraph = "http://example.org/people";

            var context = new MyEntityContext(connectionString, updateGraphUri:baseGraph, datasetGraphUris:new string[]{baseGraph});
            // Can find by property
            var alice = context.FoafPersons.FirstOrDefault(p => p.Name.Equals("Alice"));
            Assert.That(alice, Is.Not.Null);
            // Can find by ID
            alice = context.FoafPersons.FirstOrDefault(p => p.Id.Equals("alice"));
            Assert.That(alice, Is.Not.Null, "Expected to find a FoafPerson entity with Id=='alice'");
            Assert.That(alice.Name, Is.EqualTo("Alice"));
            Assert.That(alice.Knows, Is.Not.Null, "Expected a non-null value for the Knows property on the entity for Alice.");
        }

        [Test]
        public void TestInsertIntoDefaultGraph()
        {
            var storeName = "http://www.brightstardb.com/tests#empty";
            var connectionString = MakeStoreConnectionString(storeName);
            var dataObjectContext = BrightstarService.GetDataObjectContext(connectionString);

            string aliceId;
            using (var store = dataObjectContext.OpenStore(storeName))
            {
                using (var context = new MyEntityContext(store))
                {
                    var alice = context.FoafPersons.Create();
                    aliceId = alice.Id;
                    context.SaveChanges();
                }
            }
            using (var store = dataObjectContext.OpenStore(storeName))
            {
                using (var context = new MyEntityContext(store))
                {
                    var alice = context.FoafPersons.FirstOrDefault(p => p.Id.Equals(aliceId));
                    Assert.That(alice, Is.Not.Null);
                }
            }
        }

        [Test]
        public void TestDotNetRdfUpdateRoundTrip()
        {
            var storeName = "http://www.brightstardb.com/tests#empty";
            var connectionString = MakeStoreConnectionString(storeName);
            var dataObjectContext = BrightstarService.GetDataObjectContext(connectionString);
            var defaultGraph = BrightstarDB.Constants.DefaultGraphUri;

            string carolId;
            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var carol = context.FoafPersons.Create();
                    carol.Name = "Carol";
                    carol.Organisation = "Acme";
                    carolId = carol.Id;
                    context.SaveChanges();
                }
            }

            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var carol = context.FoafPersons.FirstOrDefault(p => p.Id.Equals(carolId));
                    Assert.That(carol, Is.Not.Null);
                    Assert.That(carol.Name, Is.EqualTo("Carol"));
                    Assert.That(carol.Organisation, Is.EqualTo("Acme"));
                    carol.Name = "Caroline";
                    carol.Organisation = "TechCorp";
                    context.SaveChanges();
                }
            }

            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var carol = context.FoafPersons.FirstOrDefault(p => p.Id.Equals(carolId));
                    Assert.That(carol, Is.Not.Null);
                    Assert.That(carol.Name, Is.EqualTo("Caroline"));
                    Assert.That(carol.Organisation, Is.EqualTo("TechCorp"));
                }
            }
        }

        [Test]
        public void TestDotNetRdfDeleteRoundTrip()
        {
            var storeName = "http://www.brightstardb.com/tests#empty";
            var connectionString = MakeStoreConnectionString(storeName);
            var dataObjectContext = BrightstarService.GetDataObjectContext(connectionString);
            var defaultGraph = BrightstarDB.Constants.DefaultGraphUri;

            string deleteMeId;
            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var deleteMe = context.FoafPersons.Create();
                    deleteMe.Name = "DeleteMe";
                    deleteMeId = deleteMe.Id;
                    context.SaveChanges();
                }
            }

            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var deleteMe = context.FoafPersons.FirstOrDefault(p => p.Id.Equals(deleteMeId));
                    Assert.That(deleteMe, Is.Not.Null);
                    Assert.That(deleteMe.Name, Is.EqualTo("DeleteMe"));
                    context.DeleteObject(deleteMe);
                    context.SaveChanges();
                }
            }

            using (var store = dataObjectContext.OpenStore(storeName, updateGraph: defaultGraph, defaultDataSet: new[] { defaultGraph }))
            {
                using (var context = new MyEntityContext(store))
                {
                    var deleteMe = context.FoafPersons.FirstOrDefault(p => p.Id.Equals(deleteMeId));
                    Assert.That(deleteMe, Is.Null);
                }
            }
        }

        private static string MakeStoreConnectionString(string storeName)
        {
            var configFilePath = Path.Combine(TestContext.CurrentContext.TestDirectory, Configuration.DataLocation + "dataObjectStoreConfig.ttl");
            return string.Format("type=dotNetRdf;configuration={0};storeName={1}", configFilePath, storeName);
        }
    }
}
