#nullable enable

using System;
using System.Collections.Generic;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public class StatisticsResponseModel
{
    public ulong CommitId { get; set; }
    public DateTime CommitTimestamp { get; set; }
    public ulong TotalTripleCount { get; set; }
    public Dictionary<string, ulong> PredicateTripleCounts { get; set; } = new();

    public static StatisticsResponseModel From(IStoreStatistics stats)
    {
        ArgumentNullException.ThrowIfNull(stats);

        return new StatisticsResponseModel
        {
            CommitId = stats.CommitId,
            CommitTimestamp = stats.CommitTimestamp,
            PredicateTripleCounts = stats.PredicateTripleCounts == null
                ? new Dictionary<string, ulong>()
                : new Dictionary<string, ulong>(stats.PredicateTripleCounts),
            TotalTripleCount = stats.TotalTripleCount
        };
    }
}
