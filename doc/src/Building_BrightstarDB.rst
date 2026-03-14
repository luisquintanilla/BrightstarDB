:title: Building BrightstarDB

.. _Building_BrightstarDB:

######################
 Building BrightstarDB
######################

This section describes how to build BrightstarDB from source.

.. _Build_Prerequisites:

**************
 Prerequisites
**************

1. **.NET 10 SDK** (10.0.200 or later)

   Download from https://dotnet.microsoft.com/download/dotnet/10.0

   Verify your installation::

     dotnet --version

2. **Git** — to clone the repository

.. note::

    You will require an internet connection when first building
    BrightstarDB to restore NuGet packages.

.. _Build_GettingTheSource:

*******************
 Getting The Source
*******************

Clone the repository::

  git clone https://github.com/BrightstarDB/BrightstarDB.git
  cd BrightstarDB

**Branches**

The BrightstarDB source code is organized into multiple branches:

- **develop** — latest development version
- **master** — latest stable release
- **release/X.X** — source code for named releases
- **feature/XXX** — work in progress (may be unstable)

.. _Build_Building:

**************
 Building
**************

Build the entire solution::

  dotnet build src\core\core.sln

Run all tests::

  dotnet test src\core\core.sln

The solution contains 12 projects:

====================================== ============================== ==========================
Project                                Description                    Target Framework
====================================== ============================== ==========================
BrightstarDB                           Core library (public API)      net10.0; netstandard2.0
BrightstarDB.Core                      Storage engine, SPARQL         net10.0; netstandard2.0
BrightstarDB.Server.AspNetCore         REST server (Minimal APIs)     net10.0
BrightstarDB.CodeGeneration            Roslyn code generator          net10.0
BrightstarDB.CodeGeneration.T4         T4 template package            net10.0
BrightstarDB.Tests                     Integration tests              net10.0
BrightstarDB.InternalTests             Internal / unit tests          net10.0
BrightstarDB.EntityFramework.Tests     Entity Framework unit tests    net10.0
BrightstarDB.CodeGeneration.Tests      Code generation tests          net10.0
BrightstarDB.Server.AspNetCore.Tests   Server tests                   net10.0
====================================== ============================== ==========================

.. note::

    The solution uses Central Package Management. All package versions are defined
    in ``src/core/Directory.Packages.props``. Shared build properties (such as
    ``LangVersion`` and signing) are in ``src/core/Directory.Build.props``.

.. _Build_MSBuild:

MSBuild Orchestration
=====================

An MSBuild script ``build.proj`` is provided for CI and packaging::

  dotnet msbuild build.proj /t:BuildCore           # Build core library
  dotnet msbuild build.proj /t:Test                # Run all tests
  dotnet msbuild build.proj /t:PackageCore         # Create core NuGet package
  dotnet msbuild build.proj /t:PackageCodeGeneration  # Package code generator
  dotnet msbuild build.proj /t:PackageT4           # Package T4 templates

Override the package version::

  dotnet msbuild build.proj /t:PackageCore /p:PackageVersion=2.0.0

.. _Build_BuildingTheDocumentation:

****************************
 Building The Documentation
****************************

The developer and user manual is maintained as reStructuredText files
and uses Sphinx to build::

  cd doc/src
  make html

Details on getting and using Sphinx can be found at http://sphinx-doc.org/.
Sphinx is a Python-based tool so it also requires a Python installation.

.. _Build_NuGetPackages:

****************************
 Creating NuGet Packages
****************************

Use the MSBuild ``build.proj`` targets or ``dotnet pack`` directly::

  dotnet pack src\core\BrightstarDB\BrightstarDB.csproj -c Release
  dotnet pack src\core\BrightstarDB.CodeGeneration\BrightstarDB.CodeGeneration.csproj -c Release
  dotnet pack src\core\BrightstarDB.CodeGeneration.T4\BrightstarDB.CodeGeneration.T4.csproj -c Release
