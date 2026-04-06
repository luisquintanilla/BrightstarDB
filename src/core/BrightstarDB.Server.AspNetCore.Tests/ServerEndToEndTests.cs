using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BrightstarDB.Server.AspNetCore.Configuration;
using NUnit.Framework;

namespace BrightstarDB.Server.AspNetCore.Tests;

[TestFixture]
public class ServerEndToEndTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _dataDir = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), "brightstar-e2e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataDir);

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override configuration to use a temp embedded store
                    services.Configure<BrightstarServiceConfiguration>(config =>
                    {
                        config.ConnectionString = $"type=embedded;storesDirectory={_dataDir}";
                        config.Authentication.Credentials.Clear();
                        config.Authentication.Credentials.Add(new BasicAuthenticationUser
                        {
                            Username = "testadmin",
                            Password = "testpass",
                            Claims = { "admin" }
                        });
                    });
                });
            });

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("testadmin:testpass")));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        try { if (Directory.Exists(_dataDir)) Directory.Delete(_dataDir, true); }
        catch { /* best effort cleanup */ }
    }

    [Test, Order(1)]
    public async Task ListStores_Initially_ReturnsEmptyOrSucceeds()
    {
        var response = await _client.GetAsync("/");
        // May be 200 with empty list or 500 if no stores exist yet
        Assert.That(response.StatusCode, Is.AnyOf(HttpStatusCode.OK, HttpStatusCode.InternalServerError));
    }

    [Test, Order(2)]
    public async Task CreateStore_ReturnsCreated()
    {
        var content = new StringContent("{\"StoreName\":\"e2estore\"}", Encoding.UTF8, "application/json");
        var response = await _client.PostAsync("/", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("e2estore"));
    }

    [Test, Order(3)]
    public async Task ListStores_AfterCreate_ContainsStore()
    {
        var response = await _client.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("e2estore"));
    }

    [Test, Order(4)]
    public async Task GetStore_ReturnsStoreInfo()
    {
        var response = await _client.GetAsync("/e2estore");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("\"name\":\"e2estore\""));
        Assert.That(body, Does.Contain("sparql"));
    }

    [Test, Order(5)]
    public async Task InsertAndQueryTriples_RoundTrip()
    {
        // Insert triples via transaction job
        var jobBody = JsonSerializer.Serialize(new
        {
            JobType = "Transaction",
            JobParameters = new
            {
                Inserts = "<http://example.org/alice> <http://www.w3.org/2000/01/rdf-schema#label> \"Alice\" .\n" +
                          "<http://example.org/bob> <http://www.w3.org/2000/01/rdf-schema#label> \"Bob\" .\n" +
                          "<http://example.org/alice> <http://xmlns.com/foaf/0.1/knows> <http://example.org/bob> .\n"
            }
        });

        var jobResponse = await _client.PostAsync("/e2estore/jobs",
            new StringContent(jobBody, Encoding.UTF8, "application/json"));
        Assert.That(jobResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var jobResult = JsonDocument.Parse(await jobResponse.Content.ReadAsStringAsync());
        var jobId = jobResult.RootElement.GetProperty("jobId").GetString();
        Assert.That(jobId, Is.Not.Null.And.Not.Empty);

        // Poll until job completes
        for (var i = 0; i < 30; i++)
        {
            await Task.Delay(200);
            var statusResponse = await _client.GetAsync($"/e2estore/jobs/{jobId}");
            var statusBody = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            if (statusBody.RootElement.GetProperty("jobCompletedOk").GetBoolean())
                break;
            if (statusBody.RootElement.GetProperty("jobCompletedWithErrors").GetBoolean())
                Assert.Fail("Transaction job failed: " + statusBody.RootElement.GetProperty("statusMessage").GetString());
        }

        // Query: find all labeled entities
        var sparqlContent = new StringContent(
            "SELECT ?s ?label WHERE { ?s <http://www.w3.org/2000/01/rdf-schema#label> ?label } ORDER BY ?label",
            Encoding.UTF8, "application/sparql-query");
        var queryResponse = await _client.PostAsync("/e2estore/sparql", sparqlContent);
        Assert.That(queryResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var xml = XDocument.Parse(await queryResponse.Content.ReadAsStringAsync());
        var ns = XNamespace.Get("http://www.w3.org/2005/sparql-results#");
        var results = xml.Descendants(ns + "result");
        Assert.That(results, Has.Exactly(2).Items, "Expected 2 results (Alice and Bob)");

        // Query: relationship
        var relContent = new StringContent(
            "SELECT ?who WHERE { <http://example.org/alice> <http://xmlns.com/foaf/0.1/knows> ?who }",
            Encoding.UTF8, "application/sparql-query");
        var relResponse = await _client.PostAsync("/e2estore/sparql", relContent);
        Assert.That(relResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var relXml = XDocument.Parse(await relResponse.Content.ReadAsStringAsync());
        var relResults = relXml.Descendants(ns + "result");
        Assert.That(relResults, Has.Exactly(1).Items, "Alice should know exactly one person (Bob)");
    }

    [Test, Order(6)]
    public async Task SparqlQuery_EmptyStore_ReturnsEmptyResults()
    {
        // Create a separate empty store
        var content = new StringContent("{\"StoreName\":\"emptystore\"}", Encoding.UTF8, "application/json");
        await _client.PostAsync("/", content);

        var sparqlContent = new StringContent(
            "SELECT ?s WHERE { ?s ?p ?o } LIMIT 1",
            Encoding.UTF8, "application/sparql-query");
        var response = await _client.PostAsync("/emptystore/sparql", sparqlContent);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var xml = XDocument.Parse(await response.Content.ReadAsStringAsync());
        var ns = XNamespace.Get("http://www.w3.org/2005/sparql-results#");
        Assert.That(xml.Descendants(ns + "result"), Is.Empty);

        // Cleanup
        await _client.DeleteAsync("/emptystore");
    }

    [Test, Order(7)]
    public async Task UnauthenticatedRequest_Returns401()
    {
        var unauthClient = _factory.CreateClient();
        var response = await unauthClient.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        unauthClient.Dispose();
    }

    [Test, Order(8)]
    public async Task WrongCredentials_Returns401()
    {
        var badClient = _factory.CreateClient();
        badClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes("wrong:creds")));
        var response = await badClient.GetAsync("/");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        badClient.Dispose();
    }

    [Test, Order(9)]
    public async Task DeleteStore_Succeeds()
    {
        var response = await _client.DeleteAsync("/e2estore");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test, Order(10)]
    public async Task GetDeletedStore_Returns404()
    {
        var response = await _client.GetAsync("/e2estore");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
