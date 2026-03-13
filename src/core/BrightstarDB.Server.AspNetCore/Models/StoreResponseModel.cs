#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public class StoreResponseModel
{
    /// <summary>
    /// Get or set the store name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Get or set the relative path to the commits resource
    /// </summary>
    public string Commits { get; set; } = null!;

    /// <summary>
    /// Get or set the relative path to the jobs resource
    /// </summary>
    public string Jobs { get; set; } = null!;

    /// <summary>
    /// Get or set the relative path to the transactions resource
    /// </summary>
    public string Transactions { get; set; } = null!;

    /// <summary>
    /// Get or set the relative path to the statistics resource
    /// </summary>
    public string Statistics { get; set; } = null!;

    /// <summary>
    /// Get or set the relative path to the SPARQL query endpoint
    /// </summary>
    public string SparqlQuery { get; set; } = null!;

    /// <summary>
    /// Get or set the relative pat to the SPARQL update endpoint
    /// </summary>
    public string SparqlUpdate { get; set; } = null!;

    public List<CommitPointResponseModel> CommitPoints { get; set; } = new();

    public StoreResponseModel() { }

    public StoreResponseModel(string storeName)
    {
        ArgumentNullException.ThrowIfNull(storeName);
        if (string.IsNullOrWhiteSpace(storeName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(storeName));

        Name = storeName;
        Commits = $"{storeName}/commits";
        Jobs = $"{storeName}/jobs";
        Transactions = $"{storeName}/transactions";
        Statistics = $"{storeName}/statistics";
        SparqlQuery = $"{storeName}/sparql";
        SparqlUpdate = $"{storeName}/update";
    }

    public static StoreResponseModel FromStore(string storeName, IEnumerable<ICommitPointInfo>? commitPoints = null)
    {
        var response = new StoreResponseModel(storeName);
        if (commitPoints != null)
        {
            response.CommitPoints = commitPoints.Select(CommitPointResponseModel.From).ToList();
        }

        return response;
    }
}
