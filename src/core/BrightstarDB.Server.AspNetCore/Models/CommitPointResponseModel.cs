#nullable enable

using System;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public class CommitPointResponseModel
{
    public ulong Id { get; set; }
    public string StoreName { get; set; } = null!;
    public DateTime CommitTime { get; set; }
    public Guid JobId { get; set; }

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
