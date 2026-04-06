#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record StoreResponseModel
{
    public string Name { get; init; } = null!;
    public string Commits { get; init; } = null!;
    public string Jobs { get; init; } = null!;
    public string Transactions { get; init; } = null!;
    public string Statistics { get; init; } = null!;
    public string SparqlQuery { get; init; } = null!;
    public string SparqlUpdate { get; init; } = null!;
    public List<CommitPointResponseModel> CommitPoints { get; init; } = [];

    public static StoreResponseModel FromStore(string storeName, IEnumerable<ICommitPointInfo>? commitPoints = null)
    {
        ArgumentNullException.ThrowIfNull(storeName);
        if (string.IsNullOrWhiteSpace(storeName)) throw new ArgumentException("Value cannot be null or whitespace.", nameof(storeName));

        return new StoreResponseModel
        {
            Name = storeName,
            Commits = $"{storeName}/commits",
            Jobs = $"{storeName}/jobs",
            Transactions = $"{storeName}/transactions",
            Statistics = $"{storeName}/statistics",
            SparqlQuery = $"{storeName}/sparql",
            SparqlUpdate = $"{storeName}/update",
            CommitPoints = commitPoints?.Select(CommitPointResponseModel.From).ToList() ?? []
        };
    }
}
