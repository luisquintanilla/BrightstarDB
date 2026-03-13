#nullable enable

using BrightstarDB.Client;
using BrightstarDB.Dto;

namespace BrightstarDB.Server.AspNetCore.Models;

public static class JobInfoExtensions
{
    public static JobResponseModel ToResponseModel(this IJobInfo jobInfo, string storeName)
    {
        return new JobResponseModel
        {
            JobId = jobInfo.JobId,
            Label = jobInfo.Label,
            JobStatus = jobInfo.GetJobStatusString(),
            StatusMessage = jobInfo.StatusMessage,
            StoreName = storeName,
            ExceptionInfo = jobInfo.ExceptionInfo,
            QueuedTime = jobInfo.QueuedTime,
            StartTime = jobInfo.StartTime,
            EndTime = jobInfo.EndTime
        };
    }

    public static string GetJobStatusString(this IJobInfo jobInfo)
    {
        if (jobInfo.JobPending) return "Pending";
        if (jobInfo.JobStarted) return "Started";
        if (jobInfo.JobCompletedOk) return "CompletedOk";
        if (jobInfo.JobCompletedWithErrors) return "TransactionError";
        return "Unknown";
    }
}
