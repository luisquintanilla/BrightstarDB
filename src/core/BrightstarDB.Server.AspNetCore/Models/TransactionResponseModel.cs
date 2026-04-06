#nullable enable

using System;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public record TransactionResponseModel
{
    public required string StoreName { get; init; }
    public ulong Id { get; init; }
    public required string TransactionType { get; init; }
    public required string Status { get; init; }
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
