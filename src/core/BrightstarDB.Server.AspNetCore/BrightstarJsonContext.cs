#nullable enable

using System.Text.Json.Serialization;

namespace BrightstarDB.Server.AspNetCore;

/// <summary>
/// JSON source generator context for compile-time serialization.
/// Eliminates runtime reflection and enables NativeAOT compatibility.
/// </summary>
[JsonSerializable(typeof(Dto.JobRequestObject))]
[JsonSerializable(typeof(Dto.JobResponseModel))]
[JsonSerializable(typeof(Models.StoreResponseModel))]
[JsonSerializable(typeof(Models.StoresResponseModel))]
[JsonSerializable(typeof(Models.StoreDeletedModel))]
[JsonSerializable(typeof(Models.CommitPointResponseModel))]
[JsonSerializable(typeof(Models.TransactionResponseModel))]
[JsonSerializable(typeof(Models.StatisticsResponseModel))]
[JsonSerializable(typeof(Models.CreateStoreRequestObject))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal partial class BrightstarJsonContext : JsonSerializerContext;
