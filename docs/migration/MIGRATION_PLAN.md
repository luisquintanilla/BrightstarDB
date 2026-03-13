# BrightstarDB .NET 10 Migration Plan

> **Purpose:** Primary execution document for the .NET 10 migration. Follow the phases in order.
>
> **Related docs:** [README](README.md) | [Architecture Analysis](ARCHITECTURE_ANALYSIS.md) | [EF Core Analysis](EF_CORE_ANALYSIS.md) | [Decision Log](DECISION_LOG.md) | [Knowledge Capture](KNOWLEDGE_CAPTURE.md)

## Problem Statement

BrightstarDB is an RDF/SPARQL NoSQL database for .NET with a custom Entity Framework that maps C# interfaces to RDF triples. The codebase currently targets a mix of .NET Framework 4.0–4.7.2, .NET Standard 2.0, and .NET Core 2.1. We need to modernize it to support **.NET 10 (LTS, `net10.0`)**.

**Scope:** Modernize BrightstarDB's custom Entity Framework to compile and run on .NET 10. This is NOT about creating a Microsoft EF Core provider — see [EF_CORE_ANALYSIS.md](EF_CORE_ANALYSIS.md) for why.

## Execution Order & Dependencies

```
Phase 0 (Build Infrastructure)
    ↓
Phase 1 (Core Library net10.0)  ←  MUST complete before all others
    ↓
Phase 2 (dotNetRDF upgrade)     ←  Highest risk, do early
    ↓
Phase 3 (Custom EF)  ←  Depends on Phases 1 + 2
    ↓
Phase 4 (Code Gen)   ←  Can start after Phase 1
    ↓
Phase 5 (Server)     ←  Can start after Phase 1, largest effort
    ↓
Phase 6 (Tests)      ←  Runs throughout, final verification
    ↓
Phase 7 (Legacy)     ←  Can happen anytime
    ↓
Phase 8 (CI/CD)      ←  Final phase
```

**Parallelizable:** Phases 4, 5, and 7 can run in parallel after Phase 2 completes.

---

## Phase 0: Build Infrastructure Modernization ✅ COMPLETE

**Goal:** Set up the modern .NET build foundation before touching any project code.
**Status:** Committed as part of Wave 1 (`c3cdd64c`)

### Task 0.1: Add global.json

Create `global.json` at repository root:

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

### Task 0.2: Add Directory.Build.props

Create `Directory.Build.props` at `src\core\`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <SignAssembly>true</SignAssembly>
    <AssemblyOriginatorKeyFile>$(MSBuildThisFileDirectory)..\..\key\brightstardb.snk</AssemblyOriginatorKeyFile>
  </PropertyGroup>
</Project>
```

### Task 0.3: Add Directory.Packages.props (Central Package Management)

Create `Directory.Packages.props` at `src\core\`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Core dependencies -->
    <PackageVersion Include="dotNetRdf" Version="3.5.1" />
    <PackageVersion Include="Remotion.Linq" Version="2.2.0" />
    <PackageVersion Include="Serilog" Version="4.2.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="6.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Binder" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Xml" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
    <!-- Test dependencies -->
    <PackageVersion Include="NUnit" Version="4.3.2" />
    <PackageVersion Include="NUnit3TestAdapter" Version="4.6.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="Moq" Version="4.20.72" />
    <!-- Code generation -->
    <PackageVersion Include="Buildalyzer.Workspaces" Version="7.0.2" />
    <PackageVersion Include="Microsoft.Build" Version="17.13.0" />
    <PackageVersion Include="PowerArgs" Version="3.6.0" />
  </ItemGroup>
</Project>
```

> **Important:** After creating this file, update each `.csproj` to use `<PackageReference Include="X" />` **without** the `Version` attribute. The version is now managed centrally.

### Task 0.4: Add nuget.config

Create `nuget.config` at repository root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

### Task 0.5: Clean up old NuGet artifacts

- Delete `src\core\.nuget\` folder (contains NuGet.exe 2.x, NuGet.targets — pre-PackageReference artifacts)
- Remove `.nuget` solution folder reference from `core.sln`

### Task 0.6: Modernize common.proj

- Current `common.proj` generates assembly info via MSBuild tasks
- Replace with `<Version>`, `<AssemblyVersion>`, `<FileVersion>` properties in `Directory.Build.props`
- Current version: `1.13.3.0` (common.proj) / `1.14.0` (build.proj for NuGet)
- **Bump to `2.0.0`** for the major version (breaking changes from dotNetRDF upgrade + dropping .NET Framework-only targets)

---

## Phase 1: Core Library — Add net10.0 Target ✅ COMPLETE

**Goal:** Make `BrightstarDB.Core` compile for `net10.0` while keeping `netstandard2.0` for backward compatibility.
**Status:** Committed as Wave 2 (`031e8868`). All projects retargeted. 991 tests pass.

### Task 1.1: Update BrightstarDB.Core.csproj target frameworks

Change:
```xml
<TargetFrameworks>netstandard2.0;net472</TargetFrameworks>
```
To:
```xml
<TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
```

> **Decision:** Drop `net472` as a direct target. `netstandard2.0` already covers .NET Framework 4.7.2+ consumers. Adding `net10.0` lets us use modern APIs where beneficial.

### Task 1.2: Review conditional compilation symbols

The codebase uses these `#if` symbols that need audit:

- `NETSTANDARD16` — in `TypeExtensions.cs:12`, `NamespaceDeclarations.cs:32,51`, `SparqlLinqQueryContext.cs:123`, `SparqlGeneratorWhereExpressionTreeVisitor.cs:109`
- `PORTABLE` — in multiple query files
- `NETCOREAPP10` — in test projects (**means netcoreapp1.0, NOT .NET 10!**)

**Action:** Search all `.cs` files for these symbols. Most `NETSTANDARD16` branches handle missing APIs (like `Type.GetTypeInfo()`). On `net10.0`, the non-`NETSTANDARD16` path should work. Verify and add `NET10_0` symbol if needed for .NET 10-specific code.

### Task 1.3: Update deprecated API usage

Known issues:

1. **`Uri.EscapeUriString`** — Used in `DefaultKeyConverter.cs:61` and `BrightstarEntityContext.cs:874`. Deprecated in .NET 10. Replace with `Uri.EscapeDataString` for identifier segments.
2. **`InvokeMember` reflection** — `SparqlLinqQueryContext.cs:132-133` uses late binding. Works but should be modernized to `PropertyInfo.SetValue`.
3. **`Assembly.GetCallingAssembly`** — `NamespaceDeclarations.cs:75-79`. Brittle with trimming/AOT. Consider alternative.

### Task 1.4: Handle net472-specific conditional code

In `BrightstarDB.Core.csproj`, there are conditional `<ItemGroup>` blocks for `net472`:
- Lines 33-48: References to `System.Configuration`, `System.Xml`, excluded OData files
- These blocks need `net10.0` counterparts or removal

**Action:**
- For `net10.0` target: OData files should remain excluded (they depend on WCF Data Services)
- `System.Configuration` → use `Microsoft.Extensions.Configuration` (already a dependency)
- `System.Xml` is available in `net10.0` via the base class libraries

### Task 1.5: Compile and fix

```bash
dotnet build src\core\BrightstarDB.Core\BrightstarDB.Core.csproj -f net10.0
```

Fix any compilation errors. Expected issues:
- Missing framework references for OData types (should be excluded via conditions)
- Possible `#if` path issues
- Deprecated API warnings → fix per Task 1.3

### Task 1.6: Update BrightstarDB wrapper project

`src\core\BrightstarDB\BrightstarDB.csproj` targets `netstandard2.0` only. Update to:
```xml
<TargetFrameworks>net10.0;netstandard2.0</TargetFrameworks>
```

---

## Phase 2: dotNetRDF 2.x → 3.x Upgrade ✅ COMPLETE

**Goal:** Upgrade from dotNetRDF 2.7.5 to 3.5.1. This is a **major breaking change** and the **highest-risk phase**.
**Status:** Committed as Wave 3 (`2e990754`). 166 compilation errors fixed. 28 files changed. 459 tests pass.

> ⚠️ See [KNOWLEDGE_CAPTURE.md — Pattern 8](KNOWLEDGE_CAPTURE.md#pattern-8-dotnetrdf-2x--3x-migration-checklist) for the step-by-step checklist.

### Key Breaking Changes (from official migration guide)

1. **Package restructuring** — Monolithic `dotNetRdf` split into `dotNetRdf.Core`, `dotNetRdf.Client`, `dotNetRdf.Ontology`, etc.
2. **Global statics removed** — Configuration via constructor injection/method args instead of `Options.*`
3. **Pellet reasoning & Virtuoso support dropped**
4. **Assembly signing changes** — Prebuilt packages may not be strong-named
5. **Various API changes** — Method signatures, class reorganization

### Task 2.1: Audit dotNetRDF usage in BrightstarDB.Core

**Action:** Run `grep -r "using VDS\." src\core\BrightstarDB.Core\ --include="*.cs"` and catalog every dotNetRDF type used. Group by:

- RDF model types (`IGraph`, `ITriple`, `INode`, `IUriNode`, `ILiteralNode`, etc.)
- SPARQL types (`SparqlQuery`, `SparqlResultSet`, `ISparqlQueryProcessor`, etc.)
- Parsing/serialization (`IRdfReader`, `IRdfWriter`, `TurtleParser`, etc.)
- Global options (any `Options.*` static references)

### Task 2.2: Map to new dotNetRDF 3.x packages

Based on the audit, determine which sub-packages are needed:

- `dotNetRdf.Core` — Likely sufficient for RDF model + SPARQL
- `dotNetRdf.Client` — If HTTP SPARQL endpoints are used
- Check if the monolithic `dotNetRdf` meta-package still works (it does, as a convenience)

### Task 2.3: Update package references

In `BrightstarDB.Core.csproj`:
```xml
<!-- Old -->
<PackageReference Include="dotNetRDF" Version="2.7.5" />
<!-- New -->
<PackageReference Include="dotNetRdf" Version="3.5.1" />
```

> **Note:** Package name casing changed from `dotNetRDF` to `dotNetRdf` in 3.x.

### Task 2.4: Fix compilation errors

Expected areas:
- Global static `Options.*` → constructor injection
- Namespace changes (some types moved)
- Method signature changes
- Removed types (Pellet, Virtuoso)

### Task 2.5: Verify strong naming

BrightstarDB uses strong naming (`brightstardb.snk`). dotNetRDF 3.x may not be strong-named in prebuilt packages. If this is a blocker:

- **Option A:** Build dotNetRDF from source with custom signing
- **Option B:** Drop strong naming from BrightstarDB (discuss with stakeholders)

### Task 2.6: Run core tests

```bash
dotnet test src\core\BrightstarDB.Tests\
dotnet test src\core\BrightstarDB.InternalTests\
```

Fix any SPARQL query/parsing behavior changes.

---

## Phase 3: Custom Entity Framework Modernization ✅ COMPLETE

**Goal:** Ensure the custom EF (LINQ-to-SPARQL pipeline) works correctly on .NET 10.
**Status:** All 86 EF tests pass on net10.0. LINQ-to-SPARQL pipeline works unchanged. Completed as part of Wave 2.

### Architecture Reference

```
User LINQ Query
    ↓
EntityFrameworkQueryable<T> (extends Remotion.Linq.QueryableBase<T>)
    ↓
Remotion.Linq.QueryParser.CreateDefault() → QueryModel
    ↓
EntityFrameworkQueryExecutor (implements IQueryExecutor)
    ↓
SparqlGeneratorQueryModelVisitor → walks QueryModel clauses
    ↓
SparqlQueryBuilder → builds SELECT/CONSTRUCT SPARQL strings
    ↓
BrightstarEntityContext.ExecuteQuery() → executes via store
    ↓
Results materialized back to entities/POCOs/scalars
```

### Task 3.1: Verify Remotion.Linq compatibility

Remotion.Linq 2.2.0 targets `netstandard1.0`. It should work on `net10.0` without changes.

**Action:** Write a simple test that creates an `EntityFrameworkQueryable<T>`, runs a LINQ query, and verifies SPARQL output. Run on `net10.0`.

### Task 3.2: Retarget EF test project

`BrightstarDB.EntityFramework.Tests.csproj` currently targets `net472` only. Change to:
```xml
<TargetFrameworks>net10.0;net472</TargetFrameworks>
```

Run all 96 tests on `net10.0`:
```bash
dotnet test src\core\BrightstarDB.EntityFramework.Tests\ -f net10.0
```

### Task 3.3: Fix failing LINQ-to-SPARQL tests

The 96 tests cover:
- 37 LINQ-to-SPARQL translation tests
- 10 filter optimization tests
- 10 key converter tests
- 7 DateTime function tests
- 8 string function tests
- 4 math function tests
- Various others

Fix any failures caused by:
- Expression tree differences in newer .NET runtimes
- String formatting/comparison changes
- Reflection API behavior differences

### Task 3.4: Modernize deprecated API calls in EF code

1. `Uri.EscapeUriString` in `DefaultKeyConverter.cs:61`, `BrightstarEntityContext.cs:874`
2. `InvokeMember` late binding in `SparqlLinqQueryContext.cs:132-133`
3. Clean up `NETSTANDARD16` / `PORTABLE` conditional compilation blocks

### Task 3.5: Address EntityMappingStore thread safety

`EntityMappingStore.cs` uses static dictionaries. On modern .NET with concurrent access, audit for thread safety. Consider replacing with `ConcurrentDictionary` if not already used.

### Task 3.6: Add missing test coverage

The current tests only cover LINQ translation (via `MockContext`). Add tests for:
- `BrightstarEntityContext.SaveChanges()` (with a real embedded store)
- `BrightstarEntityContext.DeleteObject()`
- `BrightstarEntitySet<T>.Add()` / `AddOrUpdate()`
- Round-trip: create entity → save → query back → verify

---

## Phase 4: Code Generation Modernization ✅ COMPLETE

**Goal:** Modernize the code generation tools to work on .NET 10.
**Status:** Committed (`1db2ee2c`). T4 retargeted from netcoreapp2.1 to net10.0. 22 CodeGen tests pass.

### Current State

| Project | Target | Status |
|---------|--------|--------|
| `BrightstarDB.CodeGeneration` | `netstandard2.0` | ✅ Already portable |
| `BrightstarDB.CodeGeneration.Console` | `net472` | ❌ Needs migration |
| `BrightstarDB.CodeGeneration.T4` | `netcoreapp2.1` | ❌ Very outdated |

### Task 4.1: Migrate CodeGeneration.Console to net10.0

Change target from `net472` to `net10.0`:
```xml
<TargetFramework>net10.0</TargetFramework>
```

Update `Microsoft.Build` and `Buildalyzer.Workspaces` packages.

### Task 4.2: Decide T4 template strategy

**Options:**
- **Option A (Recommended for now):** Use `T4.SourceGenerator` NuGet package to bridge existing T4 templates into modern builds. Low effort, maintains compatibility.
- **Option B (Future):** Migrate T4 templates to full Roslyn Incremental Source Generators. High effort, but eliminates T4 dependency entirely.

**Action:** Retarget `BrightstarDB.CodeGeneration.T4` from `netcoreapp2.1` to `net10.0`. Remove the massive commented-out old-style csproj XML. Test that T4 generation still works.

### Task 4.3: Update Buildalyzer.Workspaces

Upgrade from 2.3.0 to 7.x. Check for API changes in `Generator.cs` (main code gen entry point).

### Task 4.4: Run CodeGeneration.Tests

```bash
dotnet test src\core\BrightstarDB.CodeGeneration.Tests\ -f net10.0
```

These tests already target `net6.0` (the most modern target in the repo). Update to `net10.0`.

---

## Phase 5: Server Migration (Nancy → ASP.NET Core) ✅ COMPLETE

**Goal:** Replace the dead Nancy web framework with ASP.NET Core. This is the **largest single work item**.
**Status:** Committed (`f90045ed`). 53 new files, 3,759 lines. 9 endpoint classes, 24 routes. 976 tests pass (server test project scaffolded but empty — integration tests are Phase 6).

> ⚠️ See [KNOWLEDGE_CAPTURE.md — Pattern 7](KNOWLEDGE_CAPTURE.md#pattern-7-nancy--aspnet-core-migration-checklist) for the step-by-step checklist.

### Strategy: ASP.NET Core Minimal APIs with Route Groups

Carter was considered but rejected — Minimal APIs are first-party Microsoft-supported and provide equivalent functionality with `MapGroup()`. See [DECISION_LOG.md #3](DECISION_LOG.md).

### Task 5.1: Create new server project

Create `BrightstarDB.Server.AspNetCore` targeting `net10.0` with SDK `Microsoft.NET.Sdk.Web`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" />
  </ItemGroup>
</Project>
```

### Task 5.2: Port hosting infrastructure

Replace `Program.cs` + `Service.cs` (Nancy self-host + Windows Service) with:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(); // dual console/service mode
// ... configure services
var app = builder.Build();
// ... map endpoints
app.Run();
```

### Task 5.3: Port middleware

| Nancy Pattern | ASP.NET Core Equivalent |
|---|---|
| `BrightstarBootstrapper` (DI setup) | `builder.Services.AddXxx()` |
| `Before` pipeline (auth/permissions) | Authorization middleware + policies |
| `AfterRequest` (CORS headers) | `builder.Services.AddCors()` |
| `OnError` (JSON/text error responses) | `app.UseExceptionHandler()` or middleware |
| Nancy Basic Auth | `builder.Services.AddAuthentication().AddScheme<BasicAuthHandler>()` |
| Custom `IResponseProcessor` | ASP.NET Core `IOutputFormatter` or custom `IResult` |
| `StaticContentConventionBuilder` | `app.UseStaticFiles()` |

### Task 5.4: Port modules (recommended order)

Port in order of increasing complexity:

**1. Simple modules (1-2 routes each):**
- `DocumentationModule` → `GET /documentation`
- `LatestStatisticsModule` → `GET /{storeName}/statistics/latest`
- `StoreModule` → `GET /{storeName}`, `DELETE /{storeName}`
- `StoresModule` → `GET /`, `POST /`

**2. Paged/history modules:**
- `TransactionsModule` → `GET /{storeName}/transactions`, `GET /{storeName}/transactions/byjob/{jobId}`
- `CommitPointsModule` → `GET /{storeName}/commits`, `POST /{storeName}/commits`
- `StatisticsModule` → `GET /{storeName}/statistics`
- `JobsModule` → `GET/POST /{storeName}/jobs`, `GET /{storeName}/jobs/{jobId}`

**3. Complex modules (SPARQL/RDF content negotiation):**
- `SparqlModule` → `GET/POST /{storeName}/sparql`, `GET/POST /{storeName}/commits/{commitId}/sparql`
- `SparqlUpdateModule` → `GET/POST /{storeName}/update`
- `GraphsModule` → `GET/PUT/POST/DELETE /{storeName}/graphs`

### Task 5.5: Port custom content negotiation

This is the hardest part. Create ASP.NET Core equivalents for:

- `SparqlProcessor.cs` → Custom `IOutputFormatter` for SPARQL result formats
- `GraphListProcessor.cs` → Custom `IOutputFormatter` for graph list
- `SparqlQueryResponse.cs` → Custom `IResult` implementation
- `NegotiatorExtensions.cs` → Pagination `Link` header helper middleware

### Task 5.6: Port authentication/authorization

- Create `BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>`
- Port `IAuthenticationProvider` → ASP.NET Core authentication scheme
- Port permission providers → ASP.NET Core authorization policies
- Port `BrightstarModuleSecurity` → `[Authorize(Policy = "...")]` attributes or endpoint filters

### Task 5.7: Port tests

Replace Nancy.Testing patterns:

| Nancy Pattern | ASP.NET Core Equivalent |
|---|---|
| `Browser(new FakeBootstrapper())` | `WebApplicationFactory<Program>` |
| `browser.Get("/path")` | `client.GetAsync("/path")` |
| `.Accept("application/json")` | `request.Headers.Accept.Add(...)` |
| `.BasicAuth("user", "pass")` | `request.Headers.Authorization = ...` |
| `result.StatusCode` | `response.StatusCode` |
| `result.Body.DeserializeJson<T>()` | `response.Content.ReadFromJsonAsync<T>()` |

### Task 5.8: Decide fate of old server projects

- `BrightstarDB.Server.Modules` → **Archive** (keep in repo for reference, remove from solution)
- `BrightstarDB.Server.Runner` → **Archive**
- `BrightstarDB.Server.AspNet` → **Archive** (IIS-hosted Nancy, net4.0)
- `BrightstarDB.Server.AspNet.Secured` → **Archive** (net4.5, EF6 membership)

---

## Phase 6: Test Infrastructure Modernization

**Goal:** Get all tests running on `net10.0`.

### Task 6.1: Retarget test projects

| Project | Current | New |
|---|---|---|
| `BrightstarDB.Tests` | net472 | net10.0;net472 |
| `BrightstarDB.InternalTests` | net472 | net10.0;net472 |
| `BrightstarDB.EntityFramework.Tests` | net472 | net10.0;net472 |
| `BrightstarDB.CodeGeneration.Tests` | net6.0 | net10.0 |
| `BrightstarDB.Server.Modules.Tests` | net462 | **Replace** (new project for ASP.NET Core server) |

### Task 6.2: Fix NETCOREAPP10 confusion

In test csproj files, `NETCOREAPP10` means `netcoreapp1.0`, NOT .NET 10. The `#if NETCOREAPP10` blocks exclude certain tests on old .NET Core. Review and likely remove these conditions since `net10.0` is much more capable.

### Task 6.3: Update test framework packages

- NUnit 3.13.3 → 4.x (breaking: `Assert.That` preferred, classic asserts deprecated)
- NUnit3TestAdapter 4.4.2 → 4.6.x
- Microsoft.NET.Test.Sdk 17.5.0 → 17.12.x
- Moq 4.18.4 → 4.20.x

### Task 6.4: Run full test suite

```bash
dotnet test src\core\core.sln -f net10.0 --logger "console;verbosity=detailed"
```

Triage and fix all failures.

---

## Phase 7: Legacy Project Decisions

**Goal:** Decide what to do with projects that cannot easily migrate.

### Task 7.1: Create archived solution folder

Move all archived projects to an `archived\` solution folder in `core.sln`. Keep the code but don't build them.

### Task 7.2: Migrate simple tools

Port `BulkImport` and `Compress` to SDK-style `net10.0` console apps.

### Task 7.3: Decide on OData future

If OData support is desired, create a new `BrightstarDB.Server.OData` project using `Microsoft.AspNetCore.OData` package. This is a significant effort and should be a separate work item.

---

## Phase 8: CI/CD Modernization

**Goal:** Update build infrastructure for .NET 10.

### Task 8.1: Update appveyor.yml

Current: **Visual Studio 2017 image** (very outdated). Update to:

```yaml
image: Visual Studio 2022
environment:
  DOTNET_VERSION: '10.0'
install:
  - ps: |
      Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'
      ./dotnet-install.ps1 -Channel 10.0
build_script:
  - dotnet build src\core\core.sln -c Release
test_script:
  - dotnet test src\core\core.sln -c Release --no-build
```

Or consider migrating to **GitHub Actions** (recommended for modern projects).

### Task 8.2: Update build.proj

Modernize the MSBuild orchestration:
- Remove references to archived projects
- Update NuGet pack targets for SDK-style pack (`dotnet pack`)
- Update version to 2.0.0

### Task 8.3: Update solution file

- Remove archived projects from build configurations
- Add new ASP.NET Core server project
- Clean up duplicate `TestCaseManagementSettings` sections
- Remove `.nuget` solution folder
