#nullable enable

using System;
using System.Collections.Generic;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record StatisticsResponseModel
{
    public ulong CommitId { get; init; }
    public DateTime CommitTimestamp { get; init; }
    public ulong TotalTripleCount { get; init; }
    public Dictionary<string, ulong> PredicateTripleCounts { get; init; } = [];

    public static StatisticsResponseModel From(IStoreStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new StatisticsResponseModel
        {
            CommitId = stats.CommitId,
            CommitTimestamp = stats.CommitTimestamp,
            PredicateTripleCounts = stats.PredicateTripleCounts == null
                ? []
                : new Dictionary<string, ulong>(stats.PredicateTripleCounts),
            TotalTripleCount = stats.TotalTripleCount
        };
    }
}
