# Knowledge Capture: Migration Patterns & Lessons

> **Purpose:** Reusable migration patterns, checklists, and anti-patterns discovered during the BrightstarDB .NET 10 migration analysis. These apply broadly to any legacy .NET → modern .NET migration project.
>
> **Audience:** Any developer migrating a .NET Framework project to modern .NET. Not specific to BrightstarDB.

## Table of Contents

### Patterns (Do This)
1. [Always Verify "Entity Framework" Identity](#pattern-1-always-verify-entity-framework-identity)
2. [netstandard2.0 Libraries Are Already Mostly Compatible](#pattern-2-netstandard20-libraries-are-already-mostly-compatible)
3. [Dead Framework Dependencies Are the Real Blockers](#pattern-3-dead-framework-dependencies-are-the-real-blockers)
4. [LINQ Providers Survive Migrations Well](#pattern-4-linq-providers-survive-migrations-well)
5. [Strong Naming Creates Hidden Migration Friction](#pattern-5-strong-naming-creates-hidden-migration-friction)
6. [Test-First Migration Strategy](#pattern-6-test-first-migration-strategy)
7. [Nancy → ASP.NET Core Migration Checklist](#pattern-7-nancy--aspnet-core-migration-checklist)
8. [dotNetRDF 2.x → 3.x Migration Checklist](#pattern-8-dotnetrdf-2x--3x-migration-checklist)
9. [Build Infrastructure Modernization Checklist](#pattern-9-build-infrastructure-modernization-checklist)
10. [When NOT to Build an EF Core Provider](#pattern-10-when-not-to-build-an-ef-core-provider)

### Anti-Patterns (Don't Do This)
11. [Don't Migrate Everything at Once](#anti-pattern-dont-migrate-everything-at-once)
12. [Don't Confuse "Entity Framework" Naming with Microsoft's EF](#anti-pattern-dont-confuse-entity-framework-naming-with-microsofts-ef)

### Operational Patterns
13. [NUnit 3.x → 4.x — Know When NOT to Upgrade](#pattern-11-nunit-3x--4x--know-when-not-to-upgrade)
14. [CI/CD Modernization Checklist](#pattern-12-cicd-modernization-checklist)
15. [Legacy Tool Assessment — Archive vs Migrate vs Rewrite](#pattern-13-legacy-tool-assessment--archive-vs-migrate-vs-rewrite)

---

## Pattern 1: Always Verify "Entity Framework" Identity

**Context:** BrightstarDB uses the term "Entity Framework" extensively — it has an `EntityFramework` namespace, `EntityContext` base class, `[Entity]` attributes, and `EntityMappingStore`. This looks like Microsoft's Entity Framework at first glance.

**Lesson:** It's a completely custom implementation. BrightstarDB's EF maps C# interfaces → RDF triples via SPARQL. Microsoft's EF maps classes → relational tables via SQL. The migration strategies are fundamentally different:

| Aspect | Microsoft EF → EF Core | Custom EF Modernization |
|--------|----------------------|------------------------|
| Goal | Replace ORM framework | Retarget + update dependencies |
| Effort | Medium-High (API changes) | Low-Medium (mostly compilation) |
| Risk | Medium (behavior changes) | Low (proven code) |
| Key work | DbContext migration, LINQ provider swap | Dependency upgrades, deprecated API fixes |

**Takeaway:** Before planning any "EF to EF Core" migration, verify whether the project uses Microsoft's EF or a custom implementation. Check for:
- References to `System.Data.Entity` or `Microsoft.EntityFrameworkCore` packages
- Context classes inheriting from `DbContext` or `ObjectContext`
- Use of `DbSet<T>`
- SQL generation in the query pipeline

---

## Pattern 2: netstandard2.0 Libraries Are Already Mostly Compatible

**Context:** BrightstarDB.Core targets `netstandard2.0`, which .NET 10 implements.

**Lesson:** A `net10.0` application can already reference `netstandard2.0` libraries without any changes. Adding `net10.0` as an explicit target is about:

1. **Using newer APIs** (spans, ranges, pattern matching, etc.)
2. **Trimming/AOT support** (netstandard2.0 doesn't support trimming annotations)
3. **Official support claims** ("we support .NET 10" vs "we target netstandard2.0 which happens to work")
4. **Performance** (net10.0-specific optimizations the JIT can apply)

The actual migration work is:
- Updating NuGet dependencies to versions that support .NET 10
- Fixing deprecated API usage (e.g., `Uri.EscapeUriString`)
- Updating build infrastructure

**Takeaway:** For `netstandard2.0` libraries, the "migration" is primarily dependency upgrades + build infrastructure, not wholesale code rewrites. Don't overscope.

---

## Pattern 3: Dead Framework Dependencies Are the Real Blockers

**Context:** BrightstarDB's core library migration is relatively straightforward. The hard parts are:

| Dead Framework | Used By | Replacement | Effort |
|---------------|---------|-------------|--------|
| Nancy 1.4.5 | Server.Modules, Server.Runner | ASP.NET Core Minimal APIs | **High** — 10 modules, ~25 routes, custom content negotiation |
| WCF Data Services | OData project | ASP.NET Core OData | **High** — complete rewrite |
| WCF | Cluster (6 projects) | gRPC or custom | **Very High** — fundamental architecture change |
| OpenRasta | LinkedDataServer | ASP.NET Core | **Medium** — complete rewrite |

**Lesson:** When assessing migration effort, dead framework dependencies dominate the work. The business logic (storage engine, SPARQL, Entity Framework) migrates relatively easily. The web/communication layer is where the time goes.

**Takeaway:** During your initial assessment, identify all framework dependencies and check their .NET Core/.NET 5+ support. Dead frameworks (no .NET Core version, abandoned by maintainers) require complete rewrites and should be the primary focus of estimation.

**Quick check:** Search NuGet.org for the package. If the latest version is >3 years old and targets only `net4*` or `netstandard1.*`, it's likely dead.

---

## Pattern 4: LINQ Providers Survive Migrations Well

**Context:** BrightstarDB's Remotion.Linq-based LINQ-to-SPARQL provider is 40+ files with complex expression tree visitors.

**Lesson:** LINQ providers built on expression tree libraries tend to be runtime-agnostic because:

1. The `System.Linq.Expressions` API is stable and hasn't changed significantly across .NET versions
2. Libraries like Remotion.Linq target `netstandard1.0` — maximally portable
3. The visitor pattern is pure computation — no I/O, no platform dependencies
4. Expression trees are the same on .NET Framework and .NET 10

**Risks to watch for:**
- **String formatting changes** — different cultures or default formatting between runtimes
- **Reflection API differences** — `Type.GetTypeInfo()` vs direct `Type` properties
- **Method resolution** — edge cases in `MethodInfo.Invoke` or `DynamicInvoke`
- **Nullable reference type annotations** — new warnings but not runtime breaks

**Takeaway:** If your project has a custom LINQ provider, it's probably one of the easiest parts to migrate. Run the existing tests on the new runtime — they should mostly pass.

---

## Pattern 5: Strong Naming Creates Hidden Migration Friction

**Context:** BrightstarDB uses strong naming via `brightstardb.snk`. When upgrading dotNetRDF to 3.x, strong naming compatibility must be verified.

**The Rule:** A strong-named assembly **cannot** reference a non-strong-named assembly. This means:
- If BrightstarDB is strong-named
- And dotNetRDF 3.x ships without strong naming
- Then the build will fail at compile time

**Mitigation options (in order of preference):**
1. Check if the new version ships strong-named packages (many modern packages do)
2. Build the dependency from source with your strong name key
3. Use `ildasm`/`ilasm` to resign the assembly (fragile, not recommended)
4. Drop strong naming from your project (requires stakeholder discussion — breaking change for existing consumers)

**Takeaway:** Always check strong naming requirements early in migration planning. It's a common hidden blocker that doesn't surface until you try to compile. Search your solution for `.snk` files and `<SignAssembly>true</SignAssembly>` to know if you're affected.

---

## Pattern 6: Test-First Migration Strategy

**Context:** BrightstarDB has 96 LINQ-to-SPARQL translation tests but NO persistence/runtime tests for `SaveChanges()`, `DeleteObject()`, or `Add()`.

**Lesson:** The test-first migration strategy is:

```
1. Retarget test projects to net10.0 FIRST
2. Run existing tests — they're your regression safety net
3. Identify coverage gaps (what's NOT tested)
4. Add missing tests BEFORE making changes
5. Then upgrade dependencies and fix issues
6. Tests catch regressions immediately
```

**Why this order matters:**
- If you upgrade dependencies first and tests break, you don't know if the test was already broken or if your upgrade caused it
- Running tests first on the new runtime (before any code changes) isolates runtime-related failures from change-related failures
- Adding missing tests before changes ensures you catch regressions in previously-untested code

**Coverage gap warning signs:**
- Tests that mock out core operations (BrightstarDB's tests mock `SaveChanges()` as `NotImplementedException`)
- No integration tests or round-trip tests
- Tests only covering the "happy path"

**Takeaway:** If test coverage is uneven, invest in adding tests for the uncovered areas **before** migration, not after.

---

## Pattern 7: Nancy → ASP.NET Core Migration Checklist

**Context:** BrightstarDB.Server uses Nancy 1.4.5 with 10 modules and custom content negotiation.

**Step-by-step checklist for migrating any Nancy project to ASP.NET Core:**

### 1. Inventory
- [ ] List all `NancyModule` classes and their route definitions
- [ ] List all custom `IResponseProcessor` implementations
- [ ] List all pipeline hooks (`Before`, `After`, `OnError`)
- [ ] List all DI registrations (TinyIoC or custom bootstrapper)
- [ ] List all authentication/authorization code
- [ ] List all static file serving configuration

### 2. Create New ASP.NET Core Project
- [ ] Use `Microsoft.NET.Sdk.Web`
- [ ] Configure `WebApplication.CreateBuilder()` with equivalent DI services
- [ ] Add `UseWindowsService()` if dual console/service mode needed

### 3. Port Infrastructure (do this before routes)
- [ ] DI: TinyIoC → `builder.Services.AddXxx()`
- [ ] Auth: Nancy Basic Auth → `builder.Services.AddAuthentication().AddScheme<T>()`
- [ ] CORS: `AfterRequest` headers → `builder.Services.AddCors()`
- [ ] Error handling: `OnError` → `app.UseExceptionHandler()` or global exception middleware
- [ ] Static files: `StaticContentConventionBuilder` → `app.UseStaticFiles()`

### 4. Port Routes (simple → complex)
- [ ] Map each module to a `MapGroup()` call or a static class with endpoint methods
- [ ] Simple routes first (GET/POST with JSON)
- [ ] Paged routes next (query parameters, Link headers)
- [ ] Complex routes last (content negotiation, streaming)

### 5. Port Content Negotiation
- [ ] Custom `IResponseProcessor` → ASP.NET Core `IOutputFormatter`
- [ ] Custom response types → `IResult` implementations
- [ ] Headers/pagination → middleware or extension methods

### 6. Port Tests
| Nancy Pattern | ASP.NET Core Equivalent |
|---|---|
| `Browser(bootstrapper)` | `WebApplicationFactory<Program>` |
| `browser.Get("/path")` | `client.GetAsync("/path")` |
| `.Accept("application/json")` | `request.Headers.Accept.Add(...)` |
| `.BasicAuth("user", "pass")` | `request.Headers.Authorization = ...` |
| `result.StatusCode` | `response.StatusCode` |
| `result.Body.DeserializeJson<T>()` | `response.Content.ReadFromJsonAsync<T>()` |

### Battle-Tested Implementation Details (Phase 5)

**What we actually built:**
- 53 source files, ~3,759 lines of new code
- 9 endpoint groups covering 24 routes (14 GET, 7 POST, 1 PUT, 2 DELETE)
- 3 custom `IResult` implementations for streaming SPARQL/RDF responses
- Basic auth handler with constant-time password comparison (`CryptographicOperations.FixedTimeEquals`)

**Key decisions that worked well:**

1. **Minimal APIs over Controllers:** Minimal APIs with `MapGroup()` map 1:1 to Nancy modules. Each Nancy module becomes one static class with `MapXxxEndpoints()`. This preserved the logical grouping without the overhead of controller base classes.

2. **Endpoint filters for authorization:** Nancy's `Before` pipeline hooks that checked permissions became ASP.NET Core endpoint filters (`AddEndpointFilter`). Pattern:
   ```csharp
   group.MapGet("/", HandleGet)
       .AddSystemPermissionFilter(SystemPermissions.ListStores);
   ```

3. **IResult for streaming responses:** Nancy's custom `Response` subclasses (like `SparqlQueryResponse`) mapped directly to `IResult` implementations. The pattern is identical: write directly to `HttpContext.Response.Body`.

4. **Permission providers stayed abstract:** The `AbstractStorePermissionsProvider` / `AbstractSystemPermissionsProvider` hierarchy ported unchanged because it uses `ClaimsPrincipal` (already in Nancy via OWIN).

5. **Configuration moved to `IOptions<T>`:** Nancy's custom XML config handler became a POCO class bound to `appsettings.json` via `IOptions<BrightstarServiceConfiguration>`.

**Gotchas we encountered:**

1. **`IBrightstarService` registration:** Nancy's bootstrapper created this in `ConfigureApplicationContainer`. In ASP.NET Core, register as singleton in DI:
   ```csharp
   builder.Services.AddSingleton<IBrightstarService>(sp => {
       var config = sp.GetRequiredService<IOptions<BrightstarServiceConfiguration>>().Value;
       return BrightstarService.GetClient(config.ConnectionString);
   });
   ```

2. **SPARQL content negotiation complexity:** The `Accept` header mapping to BrightstarDB's `SparqlResultsFormat`/`RdfFormat` required a helper class (`SparqlResultFormatHelper`) that understands both SPARQL result formats (XML, JSON, CSV, TSV) and RDF graph formats (Turtle, RDF/XML, NTriples, JSON-LD). The query type (SELECT vs CONSTRUCT/DESCRIBE) determines which format family applies.

3. **Form data binding:** Nancy's `this.Bind<T>()` has no direct Minimal API equivalent. For simple models, use `[AsParameters]` or individual `[FromQuery]` parameters. For POST bodies, let the framework handle JSON deserialization. For form-encoded SPARQL, manually read `Request.ReadFormAsync()`.

4. **Paging via Link headers:** Nancy's `WithPagedList()` extension added RFC 5988 Link headers. Created a `PagingHelpers` utility class that generates `first`, `prev`, `next`, `last` links.

5. **`WindowsService` dual-mode hosting:** A single line replaces Nancy's separate `ServiceBase` subclass:
   ```csharp
   builder.Host.UseWindowsService(); // requires Microsoft.Extensions.Hosting.WindowsServices
   ```

6. **`public partial class Program;`** at the bottom of Program.cs enables `WebApplicationFactory<Program>` in the test project — without this, the test project can't reference the entry point.

**Nancy module → Minimal API mapping reference:**

| Nancy Module | ASP.NET Core Endpoint Class | Routes |
|---|---|---|
| `StoresModule` | `StoresEndpoints` | GET /, POST / |
| `StoreModule` | `StoreEndpoints` | GET/HEAD/DELETE /{storeName} |
| `CommitPointsModule` | `CommitPointsEndpoints` | GET/POST /{storeName}/commits |
| `TransactionsModule` | `TransactionsEndpoints` | GET /{storeName}/transactions |
| `StatisticsModule` + `LatestStatisticsModule` | `StatisticsEndpoints` | GET /{storeName}/statistics[/latest] |
| `JobsModule` | `JobsEndpoints` | GET/POST /{storeName}/jobs |
| `SparqlModule` | `SparqlEndpoints` | GET/POST /{storeName}/sparql |
| `SparqlUpdateModule` | `SparqlUpdateEndpoints` | POST /{storeName}/update |
| `GraphsModule` | `GraphsEndpoints` | GET/PUT/POST/DELETE /{storeName}/graphs |

---

## Pattern 8: dotNetRDF 2.x → 3.x Migration Checklist

**Context:** BrightstarDB upgraded from dotNetRDF 2.7.5 to 3.5.1. This was the highest-risk phase of the migration, requiring 28 file changes and extensive debugging. The lessons below are battle-tested.

**Step-by-step checklist:**

### 1. Audit Current Usage
- [ ] `grep -r "using VDS\." --include="*.cs"` — catalog every dotNetRDF type used
- [ ] Group by: RDF model types, SPARQL types, parsing/serialization, global options
- [ ] Check for `Options.*` static references (these are removed in 3.x)

### 2. Update Package References
- [ ] Change `dotNetRDF` → `dotNetRdf` (note casing change — NuGet is case-sensitive on some systems)
- [ ] Decide: monolithic `dotNetRdf` meta-package vs individual sub-packages:
  - `dotNetRdf.Core` — RDF model + SPARQL
  - `dotNetRdf.Client` — HTTP SPARQL endpoints
  - `dotNetRdf.Ontology` — OWL/RDFS ontology support

### 3. Fix Compilation Errors (expect 100+ errors initially)

**ISparqlDataset interface (~25 new members):**
- [ ] Add `IRefNode` overloads: `SetActiveGraph(IRefNode)`, `SetDefaultGraph(IRefNode)`, `ResetActiveGraph()`, `HasGraph(IRefNode)`, `GetModifiableGraph(IRefNode)`, `RemoveGraph(IRefNode)`, `this[IRefNode]`
- [ ] Add `ITripleIndex` members: `GetTriples(Uri)`, `GetTriples(INode)`, URI overloads of GetTriplesWithSubject/Predicate/Object, quoted triple stubs
- [ ] Most can delegate to existing implementations or return empty/throw NotSupportedException

**Node constructors — graph parameter removed:**
- [ ] `LiteralNode(IGraph, ...)` → `LiteralNode(...)` — graph is no longer a constructor param
- [ ] `UriNode(IGraph, Uri)` → `UriNode(Uri)`
- [ ] `BlankNode(IGraph, string)` → `BlankNode(string)`
- [ ] If you have custom node subclasses, update their constructors too

**Query processor — LeviathanQueryProcessor:**
- [ ] Constructor no longer accepts optimizer list directly
- [ ] Use `ConfigureOptions` callback pattern: `new LeviathanQueryProcessor(dataset, options => { ... })`
- [ ] Custom `IAlgebraOptimiser` implementations: add `UnsafeOptimisation` property (new in 3.x)
- [ ] Wrap optimizers in `SparqlOptimiser` aggregator class

**Expression evaluation — Accept pattern (BREAKING):**
- [ ] `BaseBinaryExpression.Evaluate(SparqlEvaluationContext, int)` is gone
- [ ] Implement two `Accept` methods: one for processor, one for visitor
- [ ] If using `UnknownFunction`, rewrite to extend `BaseBinaryExpression` directly

**IStorageProvider changes:**
- [ ] `Triple.GraphUri` property removed — use `Triple.Graph` (IRefNode) instead
- [ ] Add `ListGraphNames()` returning `IEnumerable<string>`
- [ ] Add `UpdateGraph(IRefNode, IEnumerable<Triple>, IEnumerable<Triple>)` overload

**RDF Handler changes:**
- [ ] `HandleTriple(Triple)` → `HandleTriple(Triple)` still exists but `HandleQuad` added
- [ ] If implementing `IRdfHandler`, add `HandleQuad(Triple, IRefNode)` method

**TriplePattern/PatternItem changes:**
- [ ] `PatternItem.VariableName` (string) → `PatternItem.Variables` (IEnumerable<string>)
- [ ] Use `.Variables.FirstOrDefault()` instead of `.VariableName`
- [ ] For existence checks: `.Variables.Any()` instead of `.VariableName != null`

**ConstantTerm changes:**
- [ ] `ConstantTerm.Evaluate(ctx, id)` → `ConstantTerm.Node` (access the node directly)

**Config/TTL loading — Graph naming (SUBTLE):**
- [ ] `GraphCollection` is now keyed by `IGraph.Name`, not `BaseUri`
- [ ] `dnr:assignUri` sets BaseUri but NOT Name in 3.x
- [ ] **FIX:** Add `dnr:withName <uri>` alongside `dnr:assignUri <uri>` in all TTL config files
- [ ] Register custom `IObjectFactory` implementations with `ConfigurationLoader`

### 4. Handle PlainLiteral vs xsd:string (THE HARDEST PART)

> **This is the #1 source of subtle test failures.** In RDF 1.1 (dotNetRDF 3.x), plain string literals are typed as `xsd:string`. But your existing data may use `rdf:PlainLiteral`. You MUST handle both.

**The problem:**
- Data stored by BrightstarDB's NTriples parser → `PlainLiteral` datatype
- Data stored by dotNetRDF 3.x parsers (Turtle, RDF/XML) → `xsd:string` datatype
- SPARQL query literals in 3.x → `xsd:string` datatype
- Datatype is part of the resource hash → `("Bob", PlainLiteral)` ≠ `("Bob", xsd:string)`

**Where to fix (each independently):**
- [ ] **Query matching** (ISparqlDataset): when searching for string literals, search BOTH `xsd:string` and `PlainLiteral`
- [ ] **Node creation**: normalize `PlainLiteral` → `xsd:string` when creating VDS nodes from store data
- [ ] **Result parsing** (`ParseLiteralString`): ensure `xsd:string` returns the expected type (string, not PlainLiteral object)
- [ ] **ContainsTriple**: must check both datatype variants

**Critical gotcha — Store.Match wildcard behavior:**
```
When Store.Match can't find a resource (FindResourceId returns NullUlong):
- Non-empty string → returns empty result set (SAFE for dual-search Concat)
- Empty string → NullUlong acts as WILDCARD, returns ALL matching triples (DANGEROUS)

Safe pattern for dual-search:
  if (string.IsNullOrEmpty(value))
      return SearchXsdStringOnly();  // Avoid wildcard duplication
  else
      return SearchXsdString().Concat(SearchPlainLiteral());  // Safe: at most one matches
```

**Test impact:**
- 16 W3C SPARQL conformance tests may fail due to RDF 1.1 behavior changes → mark `[Ignore]` with explanation
- Tests asserting `PlainLiteral` type on results → update to accept `string` type
- Tests comparing literal equality → ensure both datatypes are handled

### 5. Verify Strong Naming
- [ ] dotNetRdf 3.5.1 NuGet packages ARE strong-named ✅ (verified during BrightstarDB migration)
- [ ] If using older 3.x versions, verify individually

### 6. Test Strategy
- [ ] Run all SPARQL query tests first — these catch PlainLiteral/xsd:string issues
- [ ] Run RDF parsing/serialization tests — catches handler API changes
- [ ] Run config-based tests — catches TTL loading/graph naming issues
- [ ] Run integration tests last — catches compound issues
- [ ] **Key diagnostic:** if tests return 0 results unexpectedly, check PlainLiteral/xsd:string mismatch
- [ ] **Key diagnostic:** if tests return DOUBLE results, check Store.Match wildcard behavior with empty strings

**Actual migration metrics (BrightstarDB):**
- 166 initial compilation errors → 0 (iterative fixing)
- 28 files changed, 702 insertions, 247 deletions
- ~31 test failures → 0 (plus 16 intentionally ignored W3C conformance tests)
- Final: 459 passed, 0 failed, 59 skipped

---

## Pattern 9: Build Infrastructure Modernization Checklist

**Context:** BrightstarDB had no `global.json`, no `Directory.Build.props`, no central package management.

**Checklist for any legacy .NET project:**

### 1. Foundation Files
- [ ] Add `global.json` — pin SDK version with `rollForward: latestFeature`
- [ ] Add `nuget.config` — explicit package sources (always `<clear />` first)
- [ ] Add `Directory.Build.props` — shared properties (LangVersion, Nullable, TreatWarningsAsErrors, signing)
- [ ] Add `Directory.Packages.props` — Central Package Management

### 2. Clean Up Legacy Artifacts
- [ ] Delete `.nuget\` folder (NuGet.exe, NuGet.targets — pre-PackageReference)
- [ ] Delete `packages.config` files (replaced by PackageReference)
- [ ] Remove `.nuget` solution folder from `.sln`
- [ ] Remove `packages\` folder from `.gitignore` (no longer needed)

### 3. Convert Projects to SDK-Style
- [ ] Use `dotnet try-convert` or manual conversion
- [ ] Remove `AssemblyInfo.cs` if properties are in `.csproj` (via `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>`)
- [ ] Remove explicit file includes (SDK-style includes all by default)
- [ ] Convert `packages.config` → `<PackageReference>`

### 4. Consolidate Versions
- [ ] Move all `Version` attributes from `.csproj` to `Directory.Packages.props`
- [ ] Consolidate assembly version (`common.proj` style) into `Directory.Build.props` `<Version>` property
- [ ] Ensure version appears in exactly one place

### 5. Update CI/CD
- [ ] Update build image to support new .NET SDK
- [ ] Update build commands (`dotnet build`, `dotnet test`, `dotnet pack`)
- [ ] Remove any MSBuild restore hacks (SDK-style projects restore automatically)

---

## Pattern 10: When NOT to Build an EF Core Provider

**Context:** During BrightstarDB's migration, we evaluated whether to build an EF Core provider vs. keeping the custom Entity Framework.

**Decision Framework:**

| Factor | Build EF Core Provider | Keep Custom/Purpose-Built API |
|--------|----------------------|------------------------------|
| **Data model** | Relational or document (close to relational) | Graph, triple, key-value, time-series |
| **Query language** | SQL-like or translatable to SQL patterns | Fundamentally different (SPARQL, Cypher, Gremlin, etc.) |
| **Schema model** | Has schema or schema-like structure | Schema-less or ontology-driven |
| **Team size** | Large team with EF Core internals expertise | Small team, limited resources |
| **User base** | Large, expects EF Core familiarity | Niche, accepts purpose-built API |
| **EF Core feature coverage** | >80% of EF Core features applicable | <50% of features applicable (many "not supported") |

**How to score:**
- If your database scores "Keep Custom API" on **3 or more factors**, modernize your existing API instead of building an EF Core provider
- BrightstarDB scored "Keep Custom API" on **5 of 6 factors**

**Industry evidence:**
- ✅ Databases WITH EF Core providers: SQL Server, PostgreSQL, MySQL, SQLite, Cosmos DB, MongoDB — all relational or close-to-relational
- ❌ Databases WITHOUT EF Core providers: Neo4j, Apache Jena, Stardog, GraphDB, Redis, InfluxDB, Cassandra — all fundamentally non-relational

**Key insight:** Even Microsoft's Cosmos DB provider (built by the EF Core team itself) has severe limitations: no migrations, no schema management, no cross-document JOINs, synchronous I/O throws exceptions. If Microsoft struggles with document databases, a community team building an RDF/SPARQL provider would face far greater challenges.

**Takeaway:** Before investing 6-12 months in an EF Core provider, score your database against this framework. If the paradigm gap is too large, spend that time making your existing API excellent on modern .NET instead.

---

## Anti-Pattern: Don't Migrate Everything at Once

**Context:** BrightstarDB has ~30 projects across core, tools, cluster, and benchmarking.

**The mistake:** Trying to migrate all 30 projects to .NET 10 simultaneously. This creates:
- Overwhelming number of compilation errors
- Circular dependency issues
- Inability to test anything until everything compiles
- Demoralized developers

**The correct approach:**
1. **Core library first** — it has the most consumers and is the foundation
2. **Archive dead projects** — don't waste time migrating projects that depend on dead frameworks (WCF, Nancy on IIS, OpenRasta)
3. **Migrate incrementally** — each phase should result in a compilable, testable state
4. **Keep backward compatibility** — `netstandard2.0` alongside `net10.0` lets existing consumers keep working
5. **Parallelize independent work** — server migration (Phase 5) and code generation (Phase 4) don't depend on each other

**BrightstarDB's inventory shows why this matters:**
- 19 core projects → ~10 need migration, ~9 should be archived
- 8 tools projects → ~2 worth migrating, rest archived
- 7 cluster projects → all archived (WCF-dependent)
- 2 benchmarking projects → 1 migrated, 1 archived

Migrating 12-13 projects is manageable. Migrating 36 simultaneously is not.

---

## Anti-Pattern: Don't Confuse "Entity Framework" Naming with Microsoft's EF

**Context:** The term "Entity Framework" is generic — it means "a framework for working with entities." Multiple libraries use this term.

**The trap:** Seeing `EntityFramework` in a codebase and immediately planning a migration to EF Core. This happened during BrightstarDB's analysis — the initial approach assumed "EF to EF Core migration" before discovering it was a completely custom implementation.

**How they compare:**

| Aspect | Microsoft EF | BrightstarDB EF |
|--------|-------------|----------------|
| Context base class | `DbContext` / `ObjectContext` | `EntityContext` (abstract) / `BrightstarEntityContext` |
| Entity definition | Concrete classes | Interfaces with `[Entity]` attribute |
| Collection type | `DbSet<T>` | `IEntitySet<T>` |
| Data store | Relational databases | RDF triple store |
| Query target | SQL | SPARQL |
| Package reference | `Microsoft.EntityFrameworkCore` / `System.Data.Entity` | None (custom code in `EntityFramework` namespace) |

**Verification checklist:**
1. Does it reference `System.Data.Entity` or `Microsoft.EntityFrameworkCore`? → If NO, it's custom
2. Does the context inherit `DbContext` or `ObjectContext`? → If NO, it's custom
3. Does it generate SQL? → If NO, it's custom
4. Does it use `DbSet<T>`? → If NO, it's custom

**Impact of getting this wrong:** Planning for an EF→EF Core migration when you actually have a custom EF will produce a completely wrong plan. The custom EF migration is primarily about retargeting and updating dependencies, not replacing an ORM framework.

**Takeaway:** Always examine the actual base classes and query pipeline before planning. Don't trust namespace names or attribute names alone.

---

## Pattern 11: NUnit 3.x → 4.x — Know When NOT to Upgrade

**Context:** NUnit 4.x removes classic assert methods (`Assert.AreEqual`, `Assert.IsTrue`, `Assert.IsNotNull`) from the `Assert` class, moving them to `ClassicAssert`. This is a breaking change that affects every test file.

**Lesson:** BrightstarDB has 2,000+ classic assert calls. Upgrading to NUnit 4.x would require either:
- Bulk search-and-replace `Assert.AreEqual` → `ClassicAssert.AreEqual` (mechanical but noisy)
- Rewriting all asserts to the constraint model `Assert.That(x, Is.EqualTo(y))` (even more work)

Neither option improves test quality. NUnit 3.14.0 fully supports .NET 10.

**Decision framework for test framework upgrades:**

| Factor | Upgrade | Stay on current version |
|--------|---------|------------------------|
| Runtime compatibility | New version required for target runtime | Current version works on target runtime |
| Breaking changes | Minimal or mechanical | Massive surface area (thousands of call sites) |
| Functional benefit | New features you'll actually use | Only cosmetic/style changes |
| Migration scope | Already doing related refactoring | Test changes would be unrelated noise |

**Takeaway:** Don't upgrade test frameworks just because a new major version exists. If the current version supports your target runtime and the upgrade only brings style changes, the effort-to-value ratio is poor. Save the upgrade for a dedicated "test modernization" sprint.

---

## Pattern 12: CI/CD Modernization Checklist

**Context:** BrightstarDB's CI (AppVeyor) was configured for Visual Studio 2017 with per-project test commands. Modern .NET projects need significantly simpler CI configuration.

**CI/CD modernization steps:**

1. **Update build image** — VS2017 → VS2022 (or latest)
2. **Add SDK install step** — .NET 10 may not be pre-installed on CI images
3. **Simplify test execution** — Replace per-project `dotnet test` commands with single solution-level command
4. **Remove per-project tool installs** — Modern test adapters (NUnit3TestAdapter, Microsoft.NET.Test.Sdk) handle test reporting natively
5. **Modernize MSBuild orchestration** — Remove targets for archived projects, fix stale output paths
6. **Parameterize versions** — Use MSBuild properties for package versions instead of hardcoding

**AppVeyor-specific pattern:**
```yaml
# Before (legacy pattern):
test_script:
  - dotnet tool install --global Appveyor.TestLogger
  - cd src/core/ProjectA.Tests && dotnet test
  - cd src/core/ProjectB.Tests && dotnet test

# After (modern pattern):
test_script:
  - dotnet test src\core\core.sln --no-build --nologo -v q
```

**MSBuild orchestration pattern:**
```xml
<!-- Remove dead targets that reference archived projects -->
<!-- Parameterize versions: <PackageVersion Condition="'$(PackageVersion)'==''">2.0.0</PackageVersion> -->
<!-- Add a Test target for convenience: dotnet test on the solution -->
```

**Takeaway:** CI/CD modernization is often the lowest-risk, highest-impact phase. A simplified CI config is easier to maintain and debug. Do it last (after the code compiles and tests pass) so you have a known-good baseline.

---

## Pattern 13: Legacy Tool Assessment — Archive vs Migrate vs Rewrite

**Context:** BrightstarDB had several console tools (BulkImport, Compress), a WPF GUI (Polaris), and benchmarks targeting .NET Framework 4.0–4.5.2.

**Decision framework:**

| Factor | Archive | Migrate | Rewrite |
|--------|---------|---------|---------|
| Dead framework dependency (WCF, Nancy on IIS, OpenRasta) | ✅ | ❌ | Consider |
| SDK-style csproj? | Either | ✅ Easier | Either |
| In core solution? | Doesn't matter | ✅ | Either |
| Active users? | No | Yes | Yes |
| Modern replacement exists? | Create if needed | N/A | N/A |

**BrightstarDB examples:**
- **BulkImport/Compress** → Archive (WCF dependency = rewrite, not migrate)
- **Polaris WPF GUI** → Separate project (dotNetRDF 1.0→3.x gap too large)
- **PerformanceBenchmarks** → Archive + rewrite from scratch with BenchmarkDotNet
- **ReadWriteBenchmark** → Archive (net4.5.2 old-style project)

**Key insight:** "Migrate" implies incremental changes to existing code. If a project requires replacing its core transport layer (WCF→gRPC) or jumping 3 major dependency versions, that's a "rewrite" wearing a "migrate" costume. Be honest about the effort classification — it helps set expectations.

**Takeaway:** Don't try to migrate everything. Assess each project individually. Archive aggressively — it's better to have a clean, working core than a half-migrated tool that nobody uses.
