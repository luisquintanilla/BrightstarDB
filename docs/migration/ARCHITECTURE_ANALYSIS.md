# BrightstarDB Architecture Analysis

> **Purpose:** Document the current state of the BrightstarDB codebase as of the analysis date. This is the reference for understanding what exists today before any migration work begins.

## High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  BrightstarDB.Core (netstandard2.0 + net472)                │
│  ├── Storage Engine (B+ tree, append-only, page-based)      │
│  ├── SPARQL Query Engine (built on dotNetRDF 2.7.5)         │
│  ├── Custom Entity Framework                                │
│  │   ├── EntityContext / BrightstarEntityContext             │
│  │   ├── BrightstarEntityObject / IEntitySet<T>             │
│  │   └── LINQ-to-SPARQL (via Remotion.Linq 2.2.0)          │
│  ├── Data Object Layer                                      │
│  └── RDF Client API                                         │
├─────────────────────────────────────────────────────────────┤
│  BrightstarDB.Server.Modules (net462, Nancy 1.4)            │
│  ├── 10 NancyModule classes, ~25 REST endpoints             │
│  ├── SPARQL endpoint, Graph Store Protocol                   │
│  └── Basic auth, CORS, custom permission system             │
├─────────────────────────────────────────────────────────────┤
│  BrightstarDB.Server.Runner (net462, Nancy self-host)       │
│  └── Console app + Windows Service dual-mode                │
├─────────────────────────────────────────────────────────────┤
│  Code Generation (netstandard2.0 + net472)                  │
│  ├── Roslyn/Buildalyzer-based generator                     │
│  └── T4 template entry point (netcoreapp2.1)                │
├─────────────────────────────────────────────────────────────┤
│  Legacy Projects (net4.0–4.5, NOT SDK-style)                │
│  ├── OData (WCF Data Services)                              │
│  ├── Cluster (6 projects, WCF-based)                        │
│  ├── Polaris (WPF GUI, MvvmLight, dotNetRDF 1.0.11)        │
│  └── Tools (BulkImport, Compress, etc.)                     │
└─────────────────────────────────────────────────────────────┘
```

## Complete Project Inventory

### Core Projects (src\core\)

| Project | File | Target Framework(s) | SDK-style? | Status |
|---------|------|-------------------|-----------|--------|
| BrightstarDB.Core | `BrightstarDB.Core\BrightstarDB.Core.csproj` | `netstandard2.0;net472` | ✅ Yes | **Migrate** — add `net10.0`, drop `net472` |
| BrightstarDB | `BrightstarDB\BrightstarDB.csproj` | `netstandard2.0` | ✅ Yes | **Migrate** — add `net10.0` |
| BrightstarDB.CodeGeneration | `BrightstarDB.CodeGeneration\BrightstarDB.CodeGeneration.csproj` | `netstandard2.0` | ✅ Yes | **Keep** — already portable |
| BrightstarDB.CodeGeneration.Console | `BrightstarDB.CodeGeneration.Console\BrightstarDB.CodeGeneration.Console.csproj` | `net472` | ✅ Yes | **Migrate** — retarget to `net10.0` |
| BrightstarDB.CodeGeneration.T4 | `BrightstarDB.CodeGeneration.T4\BrightstarDB.CodeGeneration.T4.csproj` | `netcoreapp2.1` | ✅ Yes | **Migrate** — retarget to `net10.0` |
| BrightstarDB.Server.Modules | `BrightstarDB.Server.Modules\BrightstarDB.Server.Modules.csproj` | `net462` | ✅ Yes | **Archive** — Nancy-based, to be replaced |
| BrightstarDB.Server.Runner | `BrightstarDB.Server.Runner\BrightstarDB.Server.Runner.csproj` | `net462` | ✅ Yes | **Archive** — Nancy self-host, to be replaced |

### Test Projects (src\core\)

| Project | File | Target Framework(s) | SDK-style? | Status |
|---------|------|-------------------|-----------|--------|
| BrightstarDB.Tests | `BrightstarDB.Tests\BrightstarDB.Tests.csproj` | `net472` | ✅ Yes | **Migrate** — add `net10.0` |
| BrightstarDB.InternalTests | `BrightstarDB.InternalTests\BrightstarDB.InternalTests.csproj` | `net472` | ✅ Yes | **Migrate** — add `net10.0` |
| BrightstarDB.EntityFramework.Tests | `BrightstarDB.EntityFramework.Tests\BrightstarDB.EntityFramework.Tests.csproj` | `net472` | ✅ Yes | **Migrate** — add `net10.0` |
| BrightstarDB.CodeGeneration.Tests | `BrightstarDB.CodeGeneration.Tests\BrightstarDB.CodeGeneration.Tests.csproj` | `net6.0` | ✅ Yes | **Migrate** — retarget to `net10.0` |
| BrightstarDB.Server.Modules.Tests | `BrightstarDB.Server.Modules.Tests\BrightstarDB.Server.Modules.Tests.csproj` | `net462` | ✅ Yes | **Replace** — new test project for ASP.NET Core server |

### Legacy Projects — NOT SDK-Style (src\core\)

| Project | File | Target Framework | SDK-style? | Status |
|---------|------|-----------------|-----------|--------|
| BrightstarDB.OData | `BrightstarDB.OData\BrightstarDB.OData.csproj` | `net4.0` | ❌ No | **Archive** — WCF Data Services dependency |
| BrightstarDB.OData.Tests | `BrightstarDB.OData.Tests\BrightstarDB.OData.Tests.csproj` | `net4.0` | ❌ No | **Archive** — follows OData project |
| BrightstarDB.Server.AspNet | `BrightstarDB.Server.AspNet\BrightstarDB.Server.AspNet.csproj` | `net4.0` | ❌ No | **Archive** — IIS-hosted Nancy |
| BrightstarDB.Server.AspNet.Secured | `BrightstarDB.Server.AspNet.Secured\BrightstarDB.Server.AspNet.Secured.csproj` | `net4.5` | ❌ No | **Archive** — has Microsoft EF6 refs for ASP.NET membership |
| BrightstarDB.Server.IntegrationTests | `BrightstarDB.Server.IntegrationTests\BrightstarDB.Server.IntegrationTests.csproj` | `net4.5` | ❌ No | **Convert** — to SDK-style + `net10.0` |

### Tools Projects (src\tools\)

| Project | Target Framework | SDK-style? | Status |
|---------|-----------------|-----------|--------|
| BrightstarDB.BulkImport | `net4.0` | ❌ No | **Migrate** — simple console app |
| BrightstarDB.Compress | `net4.0` | ❌ No | **Migrate** — simple console app |
| SparqlTestTasks | `net4.0` | ❌ No | **Archive** — MSBuild tasks for SPARQL testing |

### Cluster Projects (src\cluster\)

| Project | Target Framework | SDK-style? | Status |
|---------|-----------------|-----------|--------|
| BrightstarDB.ClusterManager | `net4.0` | ❌ No | **Archive** — WCF-dependent |
| BrightstarDB.ClusterNode | `net4.0` | ❌ No | **Archive** — WCF-dependent |
| BrightstarDB.ClusterCommon | `net4.0` | ❌ No | **Archive** — WCF-dependent |
| BrightstarDB.Service | `net4.0` | ❌ No | **Archive** — WCF-dependent |
| BrightstarDB.Client | `net4.0` | ❌ No | **Archive** — WCF-dependent |
| BrightstarDB.DataService | `net4.0` | ❌ No | **Archive** — WCF-dependent |

### Benchmarking Projects (src\benchmarking\)

| Project | Target Framework | SDK-style? | Status |
|---------|-----------------|-----------|--------|
| PerformanceBenchmarks | `netcoreapp2.2` | ✅ Yes | **Migrate** — retarget to `net10.0` |
| ReadWriteBenchmark | `net4.5.2` | ❌ No | **Archive** — replace with modern benchmarks |

### Other

| Project | Location | Target | Status |
|---------|----------|--------|--------|
| Polaris (WPF GUI) | `src\tools\Polaris\` | `net4.0` | **Separate effort** — uses MvvmLight, dotNetRDF 1.0.11 |
| LinkedDataServer | `src\tools\LinkedDataServer\` | `net4.0` | **Archive** — uses OpenRasta (dead) |

## Build Infrastructure

### What Exists Today

| File | Location | Purpose | Modern? |
|------|----------|---------|---------|
| `build.proj` | Root | MSBuild orchestration (version 1.14.0) | ❌ Outdated |
| `common.proj` | Root | Shared assembly info generation (version 1.13.3.0) | ❌ Outdated — should use `Directory.Build.props` |
| `appveyor.yml` | Root | CI: **Visual Studio 2017 image** | ❌ Very outdated |
| `core.sln` | `src\core\` | Main solution file | ⚠️ Needs cleanup |

### What Does NOT Exist (Must Create)

| File | Purpose |
|------|---------|
| `global.json` | Pin .NET SDK version |
| `Directory.Build.props` | Shared project properties (lang version, nullable, signing) |
| `Directory.Packages.props` | Central Package Management |
| `nuget.config` | Explicit NuGet package sources |

## Dependency Compatibility Matrix

| Dependency | Current Version | Target Version | .NET 10 Compatible? | Breaking Changes? | Action |
|-----------|----------------|---------------|--------------------|--------------------|--------|
| **dotNetRDF** | 2.7.5 | 3.5.1 | ✅ (netstandard2.0) | ⚠️ **YES — Major:** package restructuring (`dotNetRDF`→`dotNetRdf`), global statics removed, API changes, Pellet/Virtuoso dropped, assembly signing changes | **Upgrade + refactor** |
| **Remotion.Linq** | 2.2.0 | 2.2.0 (latest) | ✅ (netstandard1.0) | None | **Keep as-is** |
| **Serilog** | 2.* | 4.x | ✅ | Minor | Upgrade |
| **Serilog.Sinks.Console** | 3.* | 6.x | ✅ | Minor | Upgrade |
| **Serilog.Sinks.File** | 4.* | 6.x | ✅ | Minor | Upgrade |
| **MS.Extensions.Configuration** | 7.0.x | 10.0.x | ✅ | None | Upgrade |
| **Nancy** | 1.4.5 | — | ❌ **Framework abandoned** | N/A | **Replace with ASP.NET Core** |
| **Buildalyzer.Workspaces** | 2.3.0 | 7.x | ✅ | API changes | Upgrade |
| **Microsoft.Build** | 17.5.0 | 17.13.x | ✅ | Minor | Upgrade |
| **NUnit** | 3.13.3 | 4.x | ✅ | Minor (Assert.That preferred) | Upgrade |
| **Moq** | 4.18.4 | 4.20.x | ✅ | None | Upgrade |
| **PowerArgs** | 3.6.0 | 3.6.0 (latest) | ✅ (netstandard2.0) | None | Keep |

## Custom Entity Framework Architecture

### Critical Context

BrightstarDB has its **own** Entity Framework — it is **not** Microsoft's Entity Framework. The naming similarity is coincidental. BrightstarDB's EF maps C# interfaces to RDF triples via SPARQL, not to relational database tables via SQL.

### Query Pipeline

```
User LINQ Query
    ↓
EntityFrameworkQueryable<T> (extends Remotion.Linq.QueryableBase<T>)
    ↓
Remotion.Linq.QueryParser.CreateDefault() → QueryModel
    ↓
EntityFrameworkQueryExecutor (implements Remotion.Linq.IQueryExecutor)
    ↓
SparqlGeneratorQueryModelVisitor → walks QueryModel clauses
    ↓
SparqlQueryBuilder → builds SELECT/CONSTRUCT SPARQL strings
    ↓
BrightstarEntityContext.ExecuteQuery() → executes against BrightstarDB store
    ↓
Results materialized back to entities / POCOs / scalars
```

### API Surface Inventory

| Category | Count | Key Types |
|----------|-------|-----------|
| **Context/Runtime** | 7 | `EntityContext` (abstract base), `BrightstarEntityContext` (concrete), `IEntitySet<T>`, `BrightstarEntitySet<T>`, `IEntityContextObject` |
| **Entity Base** | 5 | `BrightstarEntityObject`, `IEntityObject`, `IEntityCollection<T>`, `BrightstarEntityCollection<T>`, `LiteralsCollection<T>` |
| **Attributes** | 9 | `[Entity]`, `[Identifier]`, `[PropertyType]`, `[InverseProperty]`, `[InversePropertyType]`, `[Ignore]`, `[ClassAttribute]`, `[NamespaceDeclaration]`, `[TypeIdentifierPrefix]` |
| **Mapping/Metadata** | 6 | `EntityMappingStore` (singleton), `ReflectionMappingProvider`, `PropertyHint`, `IdentityInfo`, `IKeyConverter`, `DefaultKeyConverter` |
| **Query** | 5+ | `EntityFrameworkQueryable<T>`, `SparqlQueryContext`, `SparqlLinqQueryContext`, `SparqlOrdering`, + ~15 internal visitors |
| **Exceptions** | 6 | `EntityFrameworkException`, `UniqueConstraintViolationException`, etc. |
| **Total** | **~38 public + ~20 internal** | |

### Key Files

| File | Purpose | Lines | Notes |
|------|---------|-------|-------|
| `EntityContext.cs` | Abstract base context | ~190 | Defines contract: `SaveChanges()`, `DeleteObject()`, `ExecuteQuery()` |
| `BrightstarEntityContext.cs` | Concrete Brightstar-backed context | ~930 | Add/AddOrUpdate (L107-213), SaveChanges (L393-412), Query execution (L486-793) |
| `BrightstarEntityObject.cs` | Entity base class | ~200 | Property change tracking, `Become<T>()`, `Unbecome<T>()` |
| `EntityMappingStore.cs` | Mapping metadata singleton | ~300 | Static dictionaries — thread safety concern for modern .NET |
| `SparqlGeneratorQueryModelVisitor.cs` | LINQ→SPARQL translation | ~380 | Heart of query pipeline, walks Remotion.Linq QueryModel |
| `EntityFrameworkQueryExecutor.cs` | Bridges LINQ to SPARQL execution | ~90 | Implements `IQueryExecutor` from Remotion.Linq |
| `DefaultKeyConverter.cs` | URI↔string key conversion | ~70 | Uses deprecated `Uri.EscapeUriString` (L61) |

### Test Coverage

| Test Category | Count | Coverage Quality |
|---------------|-------|-----------------|
| LINQ-to-SPARQL translation | 37 | ✅ Good — via `MockContext` |
| Filter optimization | 10 | ✅ Good |
| Key converters | 10 | ✅ Good |
| DateTime functions | 7 | ✅ Good |
| String functions | 8 | ✅ Good |
| Math functions | 4 | ✅ Good |
| Other | 20 | ⚠️ Varies |
| **Total** | **96** | |

> ⚠️ **Gap:** All 96 tests are LINQ→SPARQL translation tests using `MockContext`. There are NO persistence/runtime tests — `SaveChanges()`, `DeleteObject()`, `Add()` all mock out as `NotImplementedException`. Round-trip tests should be added.

## Nancy Server Architecture

### Module Inventory

| Module | Route Prefix | Routes | Complexity |
|--------|-------------|--------|------------|
| `StoresModule` | `/` | GET /, POST / | Low |
| `StoreModule` | `/{storeName}` | GET, DELETE | Low |
| `SparqlModule` | `/{storeName}/sparql` | GET, POST + commit variant | **High** — SPARQL content negotiation |
| `SparqlUpdateModule` | `/{storeName}/update` | GET, POST | **High** — SPARQL Update |
| `GraphsModule` | `/{storeName}/graphs` | GET, PUT, POST, DELETE | **High** — Graph Store Protocol |
| `JobsModule` | `/{storeName}/jobs` | GET (list), POST, GET (by ID) | Medium |
| `TransactionsModule` | `/{storeName}/transactions` | GET (list), GET (by job) | Medium |
| `CommitPointsModule` | `/{storeName}/commits` | GET, POST | Medium |
| `StatisticsModule` | `/{storeName}/statistics` | GET | Low |
| `LatestStatisticsModule` | `/{storeName}/statistics/latest` | GET | Low |
| `DocumentationModule` | `/documentation` | GET | Low |

### Custom Infrastructure

| Component | Purpose | Migration Complexity |
|-----------|---------|---------------------|
| `BrightstarBootstrapper` | DI setup, pipeline configuration | Medium — map to `WebApplication.CreateBuilder()` |
| `SparqlProcessor` | SPARQL result format negotiation | **High** — custom `IOutputFormatter` needed |
| `GraphListProcessor` | Graph list format negotiation | **High** — custom `IOutputFormatter` needed |
| `BrightstarModuleSecurity` | Per-module auth/permission checks | Medium — map to ASP.NET Core authorization policies |
| `NegotiatorExtensions` | Pagination `Link` headers | Low — middleware or extension method |
| Basic Auth integration | Username/password authentication | Medium — `AuthenticationHandler<T>` |

## Known Quirks and Gotchas

These are non-obvious findings that could trip up a developer unfamiliar with the codebase:

1. **`NETCOREAPP10` means `netcoreapp1.0`, NOT .NET 10!** — Found in test projects as a conditional compilation symbol. Very confusing in the context of a .NET 10 migration.

2. **`IEntityContextObject.cs` contains a CLASS, not an interface** — Despite the `I` prefix naming convention, this file contains a class. Appears to be dead/incorrect legacy code.

3. **`BrightstarDB.Server.AspNet.Secured` has Microsoft EF6 references** — This is the ONLY project in the entire repo that references Microsoft's Entity Framework (`EntityFramework`, `EntityFramework.SqlServer`). It's used for ASP.NET membership providers, not for BrightstarDB's data model.

4. **Polaris uses ancient dotNetRDF 1.0.11.0** — While the core library uses dotNetRDF 2.7.5, the Polaris WPF GUI references an even older version (1.0.11) plus `GalaSoft.MvvmLight`.

5. **BrightstarDB.Core.csproj has conditional ItemGroups** — Lines 33-48 conditionally exclude OData files on `netstandard2.0` and include `System.Configuration` only on `net472`. These conditions must be extended for `net10.0`.

6. **Strong naming is used throughout** — All projects reference `brightstardb.snk`. When upgrading dotNetRDF to 3.x, verify the new packages are compatible with strong naming requirements.

7. **`Uri.EscapeUriString` is deprecated in .NET 10** — Used in `DefaultKeyConverter.cs:61` and `BrightstarEntityContext.cs:874`. Must be replaced.

8. **`SavingChanges` in `BrightstarEntityContext` is a FIELD, not an event** — It's typed as `EventHandler` but declared as a field, not using the `event` keyword. Unusual pattern that could cause subtle bugs with multi-subscriber scenarios.

9. **`EntityMappingStore.Instance` is a static singleton with static dictionaries** — Thread safety concern for modern .NET applications with concurrent access.

10. **Version numbers are scattered across multiple files** — `1.13.3.0` in `common.proj`, `1.14.0` in `build.proj`. Central Package Management will help but the version source-of-truth needs consolidation.

11. **The solution file (`core.sln`) has duplicate `TestCaseManagementSettings` sections** — Legacy Visual Studio cruft that should be cleaned up.
