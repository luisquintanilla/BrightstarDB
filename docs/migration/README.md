# BrightstarDB .NET 10 Migration

## What Is This?

This directory contains the complete migration plan and supporting documentation to modernize [BrightstarDB](https://github.com/BrightstarDB/BrightstarDB) — an RDF/SPARQL NoSQL database for .NET — from its current mixed .NET Framework 4.0–4.7.2 / .NET Standard 2.0 / .NET Core 2.1 targets to **.NET 10 (LTS)**.

> **Status:** Planning complete. Implementation has not started.
>
> **Branch:** `feature/net10-migration-plan`

## Key Discovery

BrightstarDB has a **custom Entity Framework** that maps C# interfaces to RDF triples via SPARQL. It does **not** use Microsoft's Entity Framework. This fundamentally shaped the migration strategy — we are modernizing the existing custom EF, not migrating to EF Core. See [EF_CORE_ANALYSIS.md](EF_CORE_ANALYSIS.md) for the full analysis.

## Document Map

| Document | Purpose | Read When... |
|----------|---------|-------------|
| **[MIGRATION_PLAN.md](MIGRATION_PLAN.md)** | The primary execution plan — 8 phases, task-by-task instructions | You need to know **what to do** |
| **[ARCHITECTURE_ANALYSIS.md](ARCHITECTURE_ANALYSIS.md)** | Current codebase state — every project, dependency, and target framework | You need to understand **what exists today** |
| **[EF_CORE_ANALYSIS.md](EF_CORE_ANALYSIS.md)** | Architecture Decision Record: Custom EF vs EF Core Provider | You want to know **why we chose this approach** |
| **[DECISION_LOG.md](DECISION_LOG.md)** | All architecture decisions with rationale and risk register | You want to understand **why specific choices were made** |
| **[KNOWLEDGE_CAPTURE.md](KNOWLEDGE_CAPTURE.md)** | Reusable migration patterns, checklists, and anti-patterns | You are **migrating another .NET project** or want lessons learned |

## How to Use These Docs

### If you're a developer executing the migration:

1. **Start with** [ARCHITECTURE_ANALYSIS.md](ARCHITECTURE_ANALYSIS.md) to understand the current codebase
2. **Execute from** [MIGRATION_PLAN.md](MIGRATION_PLAN.md) — follow the phases in order (0 → 1 → 2 → 3, then 4/5/7 can be parallelized)
3. **Reference** [DECISION_LOG.md](DECISION_LOG.md) when you encounter a "why did they choose this?" question
4. **Consult** [KNOWLEDGE_CAPTURE.md](KNOWLEDGE_CAPTURE.md) for checklists on specific migration tasks (dotNetRDF upgrade, Nancy→ASP.NET Core, etc.)

### If you're reviewing the plan:

1. Read this README for the overview
2. Read [EF_CORE_ANALYSIS.md](EF_CORE_ANALYSIS.md) for the key architecture decision
3. Skim [MIGRATION_PLAN.md](MIGRATION_PLAN.md) for the phase structure and effort estimates

### If you're migrating a different .NET project:

1. Go directly to [KNOWLEDGE_CAPTURE.md](KNOWLEDGE_CAPTURE.md) — it contains 10 reusable patterns and 3 anti-patterns that apply broadly to any legacy .NET → modern .NET migration

## Prerequisites

To execute this migration, you will need:

- **.NET 10 SDK** (10.0.100 or later)
- **Visual Studio 2022** (17.x with .NET 10 workload) or **VS Code with C# Dev Kit**
- **Git** (for branching and committing incremental progress)
- Familiarity with:
  - MSBuild / SDK-style `.csproj` files
  - NuGet package management
  - C# and LINQ
  - Basic understanding of RDF and SPARQL (for Entity Framework work)
  - ASP.NET Core Minimal APIs (for the server migration in Phase 5)

## Migration Scope Summary

### In Scope (Will Migrate)
- `BrightstarDB.Core` — the main library (storage engine, SPARQL, Entity Framework, Data Object Layer)
- `BrightstarDB` — the wrapper/packaging project
- `BrightstarDB.CodeGeneration` — Roslyn-based code generator
- `BrightstarDB.CodeGeneration.Console` — CLI code generation tool
- `BrightstarDB.CodeGeneration.T4` — T4 template wrapper
- `BrightstarDB.Server.*` — REST API server (Nancy → ASP.NET Core rewrite)
- All test projects for the above
- Build infrastructure (`global.json`, `Directory.Build.props`, CI/CD)

### Out of Scope (Will Archive)
- `BrightstarDB.OData` — depends on WCF Data Services (dead)
- `BrightstarDB.Cluster.*` (6 projects) — depends on WCF (dead for cross-platform)
- `BrightstarDB.Server.AspNet` / `BrightstarDB.Server.AspNet.Secured` — IIS-hosted Nancy (dead)
- Polaris WPF GUI — separate migration effort if needed
- `LinkedDataServer` — depends on OpenRasta (dead)
- `SparqlTestTasks` — outdated MSBuild tasks

### Versioning
The migration will bump BrightstarDB from **1.14.0** to **2.0.0** to signal breaking changes:
- dotNetRDF 2.x → 3.x (breaking API changes)
- .NET Framework-only targets dropped
- Nancy-based server replaced with ASP.NET Core
- NuGet package structure may change

## Estimated Effort

| Phase | Description | Relative Effort | Risk |
|-------|-------------|----------------|------|
| 0 | Build Infrastructure | Low | Low |
| 1 | Core Library net10.0 | Low-Medium | Low |
| 2 | dotNetRDF Upgrade | Medium | **High** |
| 3 | Custom EF Modernization | Medium | Medium |
| 4 | Code Generation | Low | Low |
| 5 | Server Migration | **High** | Medium |
| 6 | Test Infrastructure | Medium | Low |
| 7 | Legacy Project Decisions | Low | Low |
| 8 | CI/CD Modernization | Low | Low |

**Critical path:** Phase 0 → 1 → 2 → 3 → 6 (verification)
**Parallelizable after Phase 2:** Phases 4, 5, and 7

## Questions?

If anything in these documents is unclear, check [DECISION_LOG.md](DECISION_LOG.md) first — the rationale for each choice is documented there. If the question isn't answered, it may represent a gap in the analysis that should be investigated before implementing.
