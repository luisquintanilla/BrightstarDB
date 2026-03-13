#nullable enable

using System;
using BrightstarDB.Client;

namespace BrightstarDB.Server.AspNetCore.Models;

public class TransactionResponseModel
{
    /// <summary>
    /// Get the name of the store that this transaction applies to
    /// </summary>
    public string StoreName { get; set; } = null!;

    /// <summary>
    /// Get the store-unique identifier for this transaction
    /// </summary>
    public ulong Id { get; set; }

    /// <summary>
    /// Get the type of transaction
    /// </summary>
    public string TransactionType { get; set; } = null!;

    /// <summary>
    /// Get the status of the transaction
    /// </summary>
    public string Status { get; set; } = null!;

    /// <summary>
    /// Get the unique identifier of the job that processed this transaction
    /// </summary>
    public Guid JobId { get; set; }

    /// <summary>
    /// Get the date/time when processing started on the transaction
    /// </summary>
    public DateTime StartTime { get; set; }

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
