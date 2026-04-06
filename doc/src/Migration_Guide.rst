.. _Migration_Guide:

#################
 Migration Guide
#################

BrightstarDB 2.0 targets .NET 10 (LTS) and .NET Standard 2.0. For the complete
migration documentation including architecture decisions, knowledge capture, and
lessons learned, see the ``docs/migration/`` directory:

* ``docs/migration/README.md`` — Overview and navigation guide
* ``docs/migration/MIGRATION_PLAN.md`` — Phase-by-phase execution plan with commit hashes
* ``docs/migration/DECISION_LOG.md`` — 15 Architecture Decision Records
* ``docs/migration/KNOWLEDGE_CAPTURE.md`` — 13+ reusable migration patterns and anti-patterns
* ``docs/migration/EF_CORE_ANALYSIS.md`` — Analysis of why the custom Entity Framework was kept
* ``docs/migration/ARCHITECTURE_ANALYSIS.md`` — Full codebase architecture analysis

Key Changes in 2.0
===================

* Target framework: ``.NET 10`` and ``.NET Standard 2.0`` (dropped .NET Framework 4.x)
* Server: Nancy replaced with **ASP.NET Core Minimal APIs**
* dotNetRDF: Upgraded from 2.7.5 to **3.5.1**
* Removed: OData support, Polaris WPF GUI, PCL builds, Mono support
* Configuration: XML config sections replaced with ``appsettings.json``

For a complete list of breaking changes, see :ref:`What's New <Whats_New>`.
