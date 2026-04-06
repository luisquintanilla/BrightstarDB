using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BrightstarDB.Client;
using System.Numerics.Tensors;

namespace BrightstarDB.Samples.VectorSimilarity;

/// <summary>
/// Demonstrates combining BrightstarDB's RDF graph store with
/// System.Numerics.Tensors (.NET 10) for vector similarity search.
///
/// Entities are stored in BrightstarDB with text descriptions, then
/// simple bag-of-words vectors are computed and compared using
/// TensorPrimitives.CosineSimilarity — the same primitive used by
/// production vector databases and ML pipelines.
/// </summary>
class Program
{
    // Sample knowledge graph: scientists and their contributions
    private static readonly (string Uri, string Name, string Description)[] Scientists =
    [
        ("http://example.org/scientists/einstein", "Albert Einstein",
            "theoretical physics relativity quantum mechanics photon energy mass spacetime gravity"),
        ("http://example.org/scientists/curie", "Marie Curie",
            "radioactivity chemistry physics polonium radium Nobel prize radiation"),
        ("http://example.org/scientists/turing", "Alan Turing",
            "computer science mathematics cryptography artificial intelligence computation algorithm"),
        ("http://example.org/scientists/hawking", "Stephen Hawking",
            "theoretical physics black holes cosmology quantum gravity spacetime singularity radiation"),
        ("http://example.org/scientists/lovelace", "Ada Lovelace",
            "mathematics computation algorithm programming analytical engine computer science"),
        ("http://example.org/scientists/feynman", "Richard Feynman",
            "quantum mechanics physics electrodynamics particle physics Nobel prize nanotechnology"),
        ("http://example.org/scientists/noether", "Emmy Noether",
            "abstract algebra mathematics ring theory group theory physics symmetry conservation"),
        ("http://example.org/scientists/bohr", "Niels Bohr",
            "quantum mechanics physics atomic structure complementarity Copenhagen interpretation radiation"),
    ];

    static void Main()
    {
        SamplesConfiguration.Register();
        var connectionString = $"type=embedded;storesDirectory={SamplesConfiguration.StoresDirectory}";

        var client = BrightstarService.GetClient(connectionString);
        const string storeName = "VectorSimilarity";

        if (client.DoesStoreExist(storeName))
            client.DeleteStore(storeName);
        client.CreateStore(storeName);

        Console.WriteLine("=== BrightstarDB + System.Numerics.Tensors Vector Similarity Demo ===\n");

        // 1. Insert scientist data into BrightstarDB as RDF triples
        Console.WriteLine("1. Storing scientist knowledge graph in BrightstarDB...");
        var triples = string.Join("\n", Scientists.Select(s =>
            $"""
            <{s.Uri}> <http://www.w3.org/1999/02/22-rdf-syntax-ns#type> <http://example.org/ontology/Scientist> .
            <{s.Uri}> <http://xmlns.com/foaf/0.1/name> "{s.Name}" .
            <{s.Uri}> <http://example.org/ontology/description> "{s.Description}" .
            """));

        client.ExecuteTransaction(storeName, new UpdateTransactionData { InsertData = triples });
        Console.WriteLine($"   Stored {Scientists.Length} scientists with descriptions.\n");

        // 2. Query all scientists back from BrightstarDB
        Console.WriteLine("2. Querying scientists from BrightstarDB via SPARQL...");
        var sparql = """
            PREFIX foaf: <http://xmlns.com/foaf/0.1/>
            PREFIX ex: <http://example.org/ontology/>
            SELECT ?uri ?name ?desc
            WHERE {
                ?uri a ex:Scientist ;
                     foaf:name ?name ;
                     ex:description ?desc .
            }
            ORDER BY ?name
            """;

        var results = ParseSparqlResults(client.ExecuteQuery(storeName, sparql));
        Console.WriteLine($"   Retrieved {results.Count} scientists.\n");

        // 3. Build vocabulary and compute bag-of-words vectors
        Console.WriteLine("3. Computing bag-of-words vectors using System.Numerics.Tensors...");
        var vocabulary = BuildVocabulary(results.Select(r => r.Description));
        var vectors = results
            .Select(r => (r.Name, Vector: ComputeVector(r.Description, vocabulary)))
            .ToList();

        Console.WriteLine($"   Vocabulary size: {vocabulary.Count} terms");
        Console.WriteLine($"   Vector dimensionality: {vocabulary.Count}\n");

        // 4. Find most similar pairs using TensorPrimitives.CosineSimilarity
        Console.WriteLine("4. Computing pairwise cosine similarity with TensorPrimitives...\n");

        var query = "Albert Einstein";
        var queryVector = vectors.First(v => v.Name == query).Vector;

        Console.WriteLine($"   Scientists most similar to {query}:");
        Console.WriteLine($"   {"Rank",-5} {"Scientist",-25} {"Cosine Similarity",20}");
        Console.WriteLine($"   {new string('-', 50)}");

        var similarities = vectors
            .Where(v => v.Name != query)
            .Select(v => (v.Name, Similarity: TensorPrimitives.CosineSimilarity<float>(queryVector, v.Vector)))
            .OrderByDescending(x => x.Similarity)
            .ToList();

        for (int i = 0; i < similarities.Count; i++)
        {
            Console.WriteLine($"   {i + 1,-5} {similarities[i].Name,-25} {similarities[i].Similarity,20:F4}");
        }

        // 5. Find the overall most-similar pair
        Console.WriteLine("\n5. Finding most similar pair across all scientists...\n");
        var bestPair = (Name1: "", Name2: "", Similarity: float.MinValue);

        for (int i = 0; i < vectors.Count; i++)
        {
            for (int j = i + 1; j < vectors.Count; j++)
            {
                var sim = TensorPrimitives.CosineSimilarity<float>(vectors[i].Vector, vectors[j].Vector);
                if (sim > bestPair.Similarity)
                    bestPair = (vectors[i].Name, vectors[j].Name, sim);
            }
        }

        Console.WriteLine($"   Most similar pair: {bestPair.Name1} ↔ {bestPair.Name2}");
        Console.WriteLine($"   Cosine similarity: {bestPair.Similarity:F4}");
        Console.WriteLine("\n=== Demo complete ===");

        client.DeleteStore(storeName);
    }

    /// <summary>
    /// Build a vocabulary (term → index) from a set of descriptions.
    /// </summary>
    static Dictionary<string, int> BuildVocabulary(IEnumerable<string> descriptions)
    {
        return descriptions
            .SelectMany(d => d.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((term, index) => (term, index))
            .ToDictionary(x => x.term, x => x.index, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Compute a bag-of-words term frequency vector using the given vocabulary.
    /// </summary>
    static float[] ComputeVector(string description, Dictionary<string, int> vocabulary)
    {
        var vector = new float[vocabulary.Count];
        foreach (var word in description.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (vocabulary.TryGetValue(word, out var index))
                vector[index] += 1.0f;
        }
        return vector;
    }

    /// <summary>
    /// Parse SPARQL XML results into a list of (Name, Description) tuples.
    /// </summary>
    static List<(string Uri, string Name, string Description)> ParseSparqlResults(System.IO.Stream resultStream)
    {
        var doc = System.Xml.Linq.XDocument.Load(resultStream);
        var ns = System.Xml.Linq.XNamespace.Get("http://www.w3.org/2005/sparql-results#");

        return doc.Descendants(ns + "result")
            .Select(r => (
                Uri: r.Elements(ns + "binding").First(b => b.Attribute("name")?.Value == "uri").Element(ns + "uri")!.Value,
                Name: r.Elements(ns + "binding").First(b => b.Attribute("name")?.Value == "name").Element(ns + "literal")!.Value,
                Description: r.Elements(ns + "binding").First(b => b.Attribute("name")?.Value == "desc").Element(ns + "literal")!.Value
            ))
            .ToList();
    }
}
