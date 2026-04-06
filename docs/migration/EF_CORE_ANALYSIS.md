# Architecture Decision Record: Custom EF vs EF Core Provider

> **Status:** Decided — Option A (Modernize Custom EF)
>
> **Date:** 2026-03-12
>
> **Decision makers:** Project stakeholders + migration analysis team

## Context

BrightstarDB has a custom Entity Framework that maps C# interfaces to RDF triples via SPARQL. When planning the .NET 10 migration, a fundamental question arose: should we modernize this custom EF, or replace it with a proper EF Core database provider?

This document records the deep analysis performed to answer that question.

## BrightstarDB Custom EF — API Surface Inventory

Before comparing approaches, we cataloged exactly what the custom EF provides. It consists of **~38 public types + ~20 internal types** across these categories:

| Category | Types | Key Examples |
|----------|-------|-------------|
| Context/Runtime | 7 | `EntityContext` (abstract), `BrightstarEntityContext` (concrete), `IEntitySet<T>`, `BrightstarEntitySet<T>` |
| Entity Base | 5 | `BrightstarEntityObject`, `IEntityObject`, `IEntityCollection<T>`, `BrightstarEntityCollection<T>`, `LiteralsCollection<T>` |
| Attributes | 9 | `[Entity]`, `[Identifier]`, `[PropertyType]`, `[InverseProperty]`, `[InversePropertyType]`, `[Ignore]`, `[ClassAttribute]`, `[NamespaceDeclaration]`, `[TypeIdentifierPrefix]` |
| Mapping/Metadata | 6 | `EntityMappingStore`, `ReflectionMappingProvider`, `PropertyHint`, `IdentityInfo`, `IKeyConverter`, `DefaultKeyConverter` |
| Query Pipeline | 5+ internal | `EntityFrameworkQueryable<T>`, `SparqlQueryContext`, `SparqlLinqQueryContext`, + ~15 Remotion.Linq visitors |
| Exceptions | 6 | `EntityFrameworkException`, `UniqueConstraintViolationException`, etc. |

**Existing test coverage:** 96 tests for LINQ-to-SPARQL translation. Proven and stable.

## Concept Mapping: BrightstarDB EF ↔ EF Core

This table maps every major BrightstarDB EF concept to its EF Core equivalent and assesses the feasibility of bridging them:

| Concept | BrightstarDB Custom EF | EF Core | Mapping Difficulty |
|---------|----------------------|---------|-------------------|
| **Entity definition** | Interfaces with `[Entity]` attribute → code-generated implementation classes | Concrete classes with `DbSet<T>` | 🔴 **No equivalent** — EF Core has no interface-first entity model |
| **Identity** | URI-based (`[Identifier("http://...")]`) with composite key support | Simple keys (int, Guid, string) or composite keys | 🟡 **Moderate** — URI→string key adapter possible |
| **Relationships** | RDF arcs via `[PropertyType("uri")]` and `[InverseProperty]` | Navigation properties + FK conventions | 🟡 **Moderate** — conceptually similar, different mechanics |
| **Multi-type resources** | `Become<T>()` / `Unbecome<T>()` — a single RDF resource can have multiple types simultaneously | 🔴 **No equivalent at all** | 🔴 **Impossible** in EF Core — entities have exactly one CLR type |
| **Query language** | LINQ → SPARQL + raw SPARQL APIs (`ExecuteQuery(sparql)`) | LINQ → SQL + raw SQL (`FromSqlRaw()`) | 🟡 **Moderate** — LINQ layer similar; raw query APIs differ |
| **Collections** | `IEntityCollection<T>` with lazy loading, `LiteralsCollection<T>` for RDF literals | `ICollection<T>` navigation properties | 🟡 **Moderate** |
| **Named graphs** | Update graph, dataset graphs, version graph — first-class concepts | 🔴 **No equivalent** | 🔴 **No concept in EF Core** |
| **Schema model** | Schema-less (RDF ontology-driven, no DDL) | Schema-first with migrations | 🔴 **Fundamentally different** |
| **Transactions** | Append-only store with optimistic locking | ACID transactions | 🟡 **Different model** |
| **Change tracking** | `BrightstarEntityObject` property interception + `INotifyPropertyChanged` | Snapshot + proxy-based change detection | 🟡 **Different mechanism** |
| **Code generation** | T4/Roslyn generates implementation classes FROM interfaces | Scaffold from database or write classes by hand | 🔴 **Reversed direction** |
| **Upsert** | `AddOrUpdate()` is a first-class API | No built-in upsert — `Update()` ≠ upsert | 🟡 **Would need custom logic** |

**Summary:** 5 of 12 concepts have **no EF Core equivalent** (🔴). This is a fundamental paradigm mismatch, not a surface-level API difference.

## Option A: Modernize Custom EF ⭐ RECOMMENDED

**Effort:** ~2-4 weeks (Phases 1-3 of the migration plan)

### Pros

1. ✅ **Dramatically lower effort** — The custom EF works today. Just needs retargeting + dependency updates.
2. ✅ **96 existing tests** as regression safety net for LINQ-to-SPARQL translation.
3. ✅ **Remotion.Linq 2.2.0 targets netstandard1.0** — maximally portable, no update needed.
4. ✅ **Full RDF semantic fidelity** — URI identifiers, named graphs, `Become<T>()`, SPARQL features all preserved.
5. ✅ **No dependency on EF Core internal changes** — EF Core's query pipeline changes every major version (broke between 2→3→5). Provider authors must track these breaking changes.
6. ✅ **Code generation model preserved** — Interface-first → code-generated implementation is BrightstarDB's distinctive developer experience.
7. ✅ **Ships .NET 10 support faster** — Primary goal achieved without a massive rewrite.

### Cons

1. ❌ **Proprietary API** — Developers must learn `BrightstarEntityContext`, `[Entity]` attributes, etc. Knowledge doesn't transfer to other .NET projects.
2. ❌ **No EF Core ecosystem** — No migrations, no `dotnet-ef` CLI, no EF Core middleware/interceptors/third-party libraries.
3. ❌ **Permanent maintenance burden** — Team maintains the entire LINQ provider, change tracker, and mapping infrastructure.
4. ❌ **Limited community knowledge** — No Stack Overflow answers, blog posts, or third-party libraries built on this API.

## Option B: Build an EF Core Database Provider

**Effort:** ~6-12+ months for experienced engineers

### What Would Need to Be Implemented

```
Required EF Core Provider Components:
├── BrightstarDbContextOptionsExtensions         ← UseBrightstar() extension method
├── BrightstarDatabaseProviderServices           ← DI registration of all provider services
├── BrightstarQueryableMethodTranslatingVisitor   ← Translate LINQ methods → SPARQL operations
├── BrightstarSparqlTranslatingExpressionVisitor  ← Translate .NET expressions → SPARQL expressions
├── BrightstarShapedQueryCompilingVisitor         ← Compile queries → executable delegates
├── BrightstarQuerySparqlGenerator               ← Generate SPARQL strings from expression tree
├── BrightstarTypeMappingSource                  ← Map .NET types → XSD/RDF types
├── BrightstarDatabase + DatabaseCreator         ← Store lifecycle management
├── BrightstarModelValidator                     ← Validate model against RDF constraints
├── Change tracking bridge                       ← EF Core tracker → RDF triple operations
├── Model builder                                ← OnModelCreating() with RDF-specific configuration
└── Specification tests                          ← Implement EF Core's provider specification test suite
```

### Pros

1. ✅ **Familiar API** — Developers who know EF Core can use BrightstarDB immediately via `DbContext`.
2. ✅ **Ecosystem integration** — EF Core tooling, interceptors, audit frameworks all available.
3. ✅ **Community resources** — Tutorials, Stack Overflow, blog posts all apply to the general patterns.
4. ✅ **Standard patterns** — Repository pattern, Unit of Work, CQRS work naturally.
5. ✅ **Microsoft investment** — Query pipeline improvements from Microsoft benefit all providers.

### Cons

1. ❌ **MASSIVE effort** — 6-12+ months. Requires deep EF Core internals expertise. The official "writing a provider" documentation hasn't been updated since **EF Core 1.1** per Microsoft's own admission.
2. ❌ **Fundamental paradigm mismatch** — RDF triples ≠ relational tables. 5 concepts have NO EF Core equivalent (see mapping table above).
3. ❌ **Feature limitations** — Many EF Core features would need to throw "not supported" (exactly like the Cosmos DB provider: no migrations, no schema management, no cross-document JOINs, sync I/O throws exceptions).
4. ❌ **Existing 40+ EF files thrown away** — Proven, tested LINQ-to-SPARQL code replaced with new, unproven code.
5. ❌ **Interface-first model lost** — EF Core tracks concrete classes. BrightstarDB's interface-first design would need to be abandoned or wrapped with a complex adapter.
6. ❌ **`Become<T>()`/`Unbecome<T>()` impossible** — RDF multi-type semantics have zero EF Core equivalent. This feature would be permanently lost.
7. ❌ **Named graph support impossible** — No EF Core concept for RDF named graphs.
8. ❌ **Ongoing EF Core version tracking burden** — Must update for every EF Core major release. Even Microsoft's own Cosmos team has struggled to keep pace with internal query pipeline changes.
9. ❌ **No precedent exists** — Zero existing EF Core providers for RDF/SPARQL databases. The closest analogues (Cosmos, MongoDB) target document databases which are much closer to the relational model than RDF triples.
10. ❌ **Delays primary goal** — .NET 10 support pushed out 6-12 months while the provider is built.

## Option C: Hybrid Approach (Future Phase)

**What:** After achieving .NET 10 support via Option A, optionally build a thin EF Core *compatibility wrapper* (not a full provider) that delegates to the proven custom EF internals.

```csharp
// Thin adapter — NOT a real EF Core provider, but provides familiar-ish syntax
public class BrightstarDbContext : IDisposable
{
    private readonly BrightstarEntityContext _inner;
    public BrightstarDbSet<IFilm> Films => new(_inner.Films);
    public BrightstarDbSet<IActor> Actors => new(_inner.Actors);

    public void SaveChanges() => _inner.SaveChanges();
}

public class BrightstarDbSet<T> : IQueryable<T> where T : class
{
    private readonly IEntitySet<T> _inner;
    public void Add(T entity) => _inner.Add(entity);
    // Delegates LINQ queries to existing LINQ-to-SPARQL pipeline
}
```

### Pros

- Familiar API surface without 6-12 month rewrite
- Reuses proven LINQ-to-SPARQL internals (zero risk to query correctness)
- Can be done in 2-4 weeks after .NET 10 migration
- Preserves RDF-specific features alongside familiar surface
- Low risk — it's an adapter layer, not a replacement

### Cons

- Not a true EF Core provider (no `DbContext` extension, no DI integration, no interceptors)
- EF Core tooling (`dotnet-ef`) won't work
- Could confuse developers expecting full EF Core behavior
- Adds a maintenance surface (thin, but still exists)

**Effort:** ~2-4 weeks after Option A completes

## Decision

### → Option A (Modernize Custom EF) for this migration project.

### Rationale

1. **The primary goal is .NET 10 support, not API redesign.** Option A delivers this in weeks, not months.
2. **The RDF-to-relational paradigm gap makes a real EF Core provider impractical.** 5 of 12 core concepts have NO EF Core mapping. The resulting provider would have more "not supported" exceptions than working features.
3. **The existing code is proven and tested.** 96 LINQ-to-SPARQL tests and a working LINQ provider built on Remotion.Linq (netstandard1.0). Throwing this away for unproven code is high-risk.
4. **No precedent exists for RDF/SPARQL EF Core providers.** Neo4j, Apache Jena, Stardog, and GraphDB all provide purpose-built client APIs. This is the accepted pattern for non-relational databases with fundamentally different query models.
5. **Option C provides a future path for familiar syntax** if there's user demand, without the risk and effort of a full provider.

### Industry Context

Microsoft's own guidance acknowledges the limitations of EF Core for non-relational data:
- The **Cosmos DB provider** (Microsoft-built) lacks migrations, schema management, cross-document JOINs, and throws on synchronous I/O
- The **MongoDB provider** (community-built, later adopted) has similar limitations
- The official "writing a provider" documentation was last substantially updated for **EF Core 1.1**

For databases whose data models are fundamentally different from relational tables (graph databases, triple stores, key-value stores, time-series databases), purpose-built APIs are the industry standard approach.

## Comparison Summary

| Factor | Option A: Custom EF | Option B: EF Core Provider | Option C: Hybrid |
|--------|---------------------|---------------------------|------------------|
| **Effort** | ~2-4 weeks | ~6-12+ months | ~2-4 weeks (after A) |
| **Risk** | Low | Very High | Low |
| **RDF fidelity** | Full | Severely limited | Full |
| **.NET 10 timeline** | Immediate | Delayed 6-12 months | After A completes |
| **API familiarity** | BrightstarDB-specific | EF Core standard | Familiar-ish |
| **`Become<T>()` support** | ✅ Yes | ❌ Impossible | ✅ Yes |
| **Named graphs** | ✅ Yes | ❌ Impossible | ✅ Yes |
| **Ecosystem tools** | ❌ None | ✅ Full | ❌ None |
| **Maintenance burden** | Medium (existing) | Very High (new + tracking EF Core changes) | Low (thin adapter) |
| **Precedent** | ✅ All RDF databases do this | ❌ Zero RDF/SPARQL EF Core providers exist | ⚠️ Uncommon but reasonable |
