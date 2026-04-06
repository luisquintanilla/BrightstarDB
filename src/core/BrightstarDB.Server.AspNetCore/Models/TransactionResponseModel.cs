#nullable enable

using System;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record TransactionResponseModel
{
    public string StoreName { get; init; } = null!;
    public ulong Id { get; init; }
    public string TransactionType { get; init; } = null!;
    public string Status { get; init; } = null!;
    public Guid JobId { get; init; }
    public DateTime StartTime { get; init; }

    public static TransactionResponseModel From(ITransactionInfo transactionInfo)
    {
        ArgumentNullException.ThrowIfNull(transactionInfo);

        return new TransactionResponseModel
        {
            Id = transactionInfo.Id,
            JobId = transactionInfo.JobId,
            StoreName = transactionInfo.StoreName,
            StartTime = transactionInfo.StartTime,
            Status = transactionInfo.Status.ToString(),
            TransactionType = transactionInfo.TransactionType.ToString()
        };
    }
}
