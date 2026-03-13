#nullable enable

using System;

namespace BrightstarDB.Server.AspNetCore.Authorization;

[Flags]
public enum StorePermissions
{
    None = 0x0,
    Read = 0x01,
    Export = 0x02,
    ViewHistory = 0x04,
    SparqlUpdate = 0x10,
    TransactionUpdate = 0x20,
    Admin = 0x4000,
    WithGrant = 0x8000,
    All = Read | Export | ViewHistory | SparqlUpdate | TransactionUpdate | Admin | WithGrant
}
