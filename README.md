BrightstarDB
============

[![Build status](https://ci.appveyor.com/api/projects/status/s8cinpfl2dh6y31a/branch/develop?svg=true)](https://ci.appveyor.com/project/kal/brightstardb/branch/develop)


BrightstarDB is a native .NET RDF triple store targeting .NET 10 and .NET Standard 2.0. It uses dotNetRDF to provide support for 
a wide range of RDF syntaxes as well as SPARQL query support. In addition to providing
a raw RDF-based API, BrightstarDB also provides support for binding RDF resources to
.NET dynamic objects; and a contract-first entity framework that enables the use of
LINQ rather than SPARQL for query purposes.

For details and documentation, please see the ``doc/src/`` directory (Sphinx/reStructuredText format).

To get started with BrightstarDB you may want to check out the following resources:
 * [Developer Quick Start](doc/src/Developer_Quick_Start.rst) provides a step-by-step introduction
 * [Entity Framework Guide](doc/src/Entity_Framework.rst) covers the full entity framework
 * [Migration Documentation](docs/migration/) documents the .NET 10 migration plan, decisions, and lessons learned

Building
--------

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0):

```
dotnet build src\core\core.sln
dotnet test src\core\core.sln
```

Licensing
---------

BrightstarDB is provided under the MIT license. It is free to use for both commercial and non-commercial purposes.


Questions ?
-----------

If you have questions please raise them on StackOverflow and tag your question `brightstardb`

Bugs ?
------

Please report any bugs you find here on the [GitHub issue tracker](https://github.com/BrightstarDB/BrightstarDB/issues).
