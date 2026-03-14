.. _Known_Issues:

#############
 Known Issues
#############

.. _http://www.w3.org/TR/WD-html40-970708/sgml/entities.html: http://www.w3.org/TR/WD-html40-970708/sgml/entities.html



***************
 SPARQL Queries
***************




When using the less-than (<) symbol in SPARQL queries, it is necessary to put spaces between the symbol and the rest of the query to avoid a parser error. For example the following query will fail with a parser error:::

  SELECT ?p ?s WHERE { ?p a <http://example.org/schema/person> . ?p <http://example.org/schema/salary> ?s . **FILTER (?s<50000)**  } 

but the same query written as shown below will be processed correctly.::

  SELECT ?p ?s WHERE { ?p a <http://example.org/schema/person> . ?p <http://example.org/schema/salary> ?s . **FILTER (?s < 50000)**  }




*************************
 Entity Framework Tooling
*************************


'_' underscore characters are not allowed in the names of the namespace(s) containing the interfaces that are to be generated into entity classes.



The T4 text template for entity generation works with Visual Studio 2022 and later.
Alternatively, use the console-based Roslyn code generator
(``BrightstarDB.CodeGeneration.Console``) which works without Visual Studio.








*******************************************
 Avoid HTML Named Entities in String Values
*******************************************


Using HTML named entities in string values that are not also valid XML named entities will result in errors when parsing the SPARQL results if these string values are included in the results set. Examples of such entities are &pound; for a pound-symbol, &copy; for a copyright symbol etc. It is best to avoid this situation by converting all HTML named entities to their numeric entity form before storing them in BrightstarDB (e.g. &#163; instead of &pound;). A full list of HTML named entities and their numeric equivalents for HTML 4 can be found at `http://www.w3.org/TR/WD-html40-970708/sgml/entities.html`_.