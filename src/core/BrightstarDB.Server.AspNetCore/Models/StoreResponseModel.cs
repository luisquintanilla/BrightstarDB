#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record StoreResponseModel
{
    public required string Name { get; init; }
    public required string Commits { get; init; }
    public required string Jobs { get; init; }
    public required string Transactions { get; init; }
    public required string Statistics { get; init; }
    public required string SparqlQuery { get; init; }
    public required string SparqlUpdate { get; init; }
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
