#nullable enable
using BrightstarDB.Storage;

namespace BrightstarDB.Server.AspNetCore.Models;

public class CreateStoreRequestObject
{
    public string StoreName { get; set; } = null!;
    public int PersistenceType { get; set; }

    public CreateStoreRequestObject() { }

    public CreateStoreRequestObject(string storeName) : this(storeName, null) { }

    public CreateStoreRequestObject(string storeName, PersistenceType? persistenceType)
    {
        StoreName = storeName;
        PersistenceType = persistenceType.HasValue ? (int)persistenceType : -1;
    }

    public PersistenceType? GetBrightstarPersistenceType() => PersistenceType switch
    {
        0 => Storage.PersistenceType.AppendOnly,
        1 => Storage.PersistenceType.Rewrite,
        _ => null
    };
}
