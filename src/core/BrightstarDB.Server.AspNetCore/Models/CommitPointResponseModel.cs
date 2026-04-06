#nullable enable

using System;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record CommitPointResponseModel
{
    public ulong Id { get; init; }
    public string StoreName { get; init; } = null!;
    public DateTime CommitTime { get; init; }
    public Guid JobId { get; init; }

    public static CommitPointResponseModel From(ICommitPointInfo commitPointInfo)
    {
        ArgumentNullException.ThrowIfNull(commitPointInfo);

        return new CommitPointResponseModel
        {
            Id = commitPointInfo.Id,
            StoreName = commitPointInfo.StoreName,
            CommitTime = commitPointInfo.CommitTime,
            JobId = commitPointInfo.JobId
        };
    }
}
