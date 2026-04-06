.. _Running_BrightstarDB:

#######################
 Running BrightstarDB
#######################

BrightstarDB can be used as an embedded database or accessed via HTTP(S) as a RESTful
web service. The REST service is an ASP.NET Core application that can run as a
standalone server, a Windows Service, behind IIS as a reverse proxy, or in a Docker container.

*********************************************
 Running BrightstarDB as a Windows Service
*********************************************

The BrightstarDB ASP.NET Core server supports running as a Windows Service
using ``Microsoft.Extensions.Hosting.WindowsServices``. To install as a service::

  sc create BrightstarDB binPath="C:\path\to\BrightstarDB.Server.AspNetCore.exe"
  sc start BrightstarDB

The server reads configuration from ``appsettings.json`` in the application directory.
See the `BrightstarDB Service Configuration`_ section below for details.

*****************************************
 Running BrightstarDB as an Application
*****************************************

Run the server directly from the command line::

  dotnet run --project src\core\BrightstarDB.Server.AspNetCore

Or from a published build::

  BrightstarDB.Server.AspNetCore.exe

By default the server listens on ``http://localhost:5000``. Configure the
URL with the ``--urls`` parameter or ``ASPNETCORE_URLS`` environment variable::

  BrightstarDB.Server.AspNetCore.exe --urls "http://0.0.0.0:8090"

***********************************
 Running BrightstarDB Behind IIS
***********************************

BrightstarDB's ASP.NET Core server can run behind IIS as a reverse proxy
using the ASP.NET Core Module. See the Microsoft documentation:
https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/

For most deployments, running as a standalone Kestrel server or Windows Service
is recommended.

********************************
 Running BrightstarDB in Docker
********************************

Create a Dockerfile for the ASP.NET Core server::

  FROM mcr.microsoft.com/dotnet/aspnet:10.0
  WORKDIR /app
  COPY publish/ .
  EXPOSE 8090
  ENTRYPOINT ["dotnet", "BrightstarDB.Server.AspNetCore.dll", "--urls", "http://0.0.0.0:8090"]

Build and run::

  dotnet publish src/core/BrightstarDB.Server.AspNetCore -c Release -o publish
  docker build -t brightstardb .
  docker run -p 8090:8090 -v brightstar-data:/data brightstardb

***********************************
 BrightstarDB Service Configuration
***********************************

The BrightstarDB server is configured via ``appsettings.json`` using the
``BrightstarService`` section.

Sample ``appsettings.json``::

  {
    "BrightstarService": {
      "ConnectionString": "type=embedded;StoresDirectory=/data/brightstar",
      "Authentication": {
        "BasicAuthRealm": "BrightstarDB",
        "Credentials": [
          {
            "Username": "admin",
            "Password": "your-password-here",
            "Claims": ["admin"]
          }
        ]
      },
      "StorePermissions": {
        "Authenticated": "All",
        "Anonymous": "Read"
      },
      "SystemPermissions": {
        "Authenticated": "All",
        "Anonymous": "ListStores"
      },
      "Cors": {
        "DisableCors": false,
        "AllowOrigin": "*"
      }
    }
  }

Configuration Properties
========================

``BrightstarService``
  Root configuration section.

  ``ConnectionString`` : string
    BrightstarDB connection string. Default uses an embedded store.
    Example: ``"type=embedded;StoresDirectory=c:\\brightstar"``

``Authentication``
  Controls HTTP Basic Authentication.

  ``BasicAuthRealm`` : string
    The realm name returned in ``WWW-Authenticate`` headers. Default: ``"BrightstarDB"``

  ``Credentials`` : array of objects
    Each entry defines a user with the following properties:

    - ``Username`` : string — the login username
    - ``Password`` : string — the login password
    - ``Claims`` : array of strings — permission claims assigned to this user

``StorePermissions``
  Default permissions for store-level operations.

  ``Authenticated`` : string — permissions for authenticated users. Default: ``"None"``

  ``Anonymous`` : string — permissions for unauthenticated users. Default: ``"None"``

  Valid values: ``None``, ``Read``, ``Export``, ``ViewHistory``,
  ``SparqlUpdate``, ``TransactionUpdate``, ``Admin``, ``All``

``SystemPermissions``
  Default permissions for system-level operations.

  ``Authenticated`` : string — permissions for authenticated users. Default: ``"None"``

  ``Anonymous`` : string — permissions for unauthenticated users. Default: ``"None"``

  Valid values: ``None``, ``ListStores``, ``CreateStore``, ``Admin``, ``All``

``Cors``
  Cross-Origin Resource Sharing configuration.

  ``DisableCors`` : bool — set to ``true`` to disable CORS entirely. Default: ``false``

  ``AllowOrigin`` : string — single allowed origin. Default: ``"*"``

  ``AllowedOrigins`` : array of strings — multiple specific allowed origins

  ``AllowedHeaders`` : array of strings — allowed request headers

  ``AllowedMethods`` : array of strings — allowed HTTP methods

  ``AllowCredentials`` : bool — whether to allow credentials in CORS requests

.. _Caching:

*********************
 Configuring Caching
*********************

BrightstarDB provides facilities for caching the results of SPARQL queries both in memory and to disk.
Caching complex SPARQL queries or queries that potentially return large numbers of results can provide
a significant performance improvement. Caching is controlled through a combination of settings in the 
application configuration file (the web.config for web apps, or the .exe.config for other executables).

**AppSetting Key**  **Default Value**  **Description**  
BrightstarDB.EnableQueryCache  false  Boolean value ("true" or "false") that specifies if the system should cache the result of SPARQL queries.  
BrightstarDB.QueryCacheMemory  256  The size in MB of the in-memory query cache.  
BrightstarDB.QueryCacheDirectory  <undefined>  The path to the directory to be used for the disk cache. If left undefined, then the behaviour depends on whether the BrightstarDB.StoreLocation setting is provided. If it is, then a disk cache will be created in the _bscache subdirectory of the StoreLocation, otherwise disk caching will be disabled.  
BrightstarDB.QueryCacheDiskSpace  2048  The size in MB of the disk cache.  

Example Caching Configurations
==============================

To cache in the _bscache subdirectory of a fixed store location (a good choice for server 
applications), it is necessary only to enable caching and ensure that the store location 
is specified in the configuration file::

  <configuration>
    <appSettings>
      <add key="BrightstarDB.EnableQueryCache" value="true" />
      <!-- disk cache will be written to the directory d:\brightstar\_bscache -->
      <add key="BrightstarDB.StoreLocation" value="d:\brightstar\" />
    </appSettings>
  </configuration>


To cache in some other location (e.g. a fast disk dedicated to caching)::

  <configuration>
    <configSections>
      <section name="brightstarService" type="BrightstarDB.Server.Modules.BrightstarServiceConfigurationSectionHandler, BrightstarDB.Server.Modules"/>
    </configSections>
    <appSettings>
      <add key="BrightstarDB.EnableQueryCache" value="true" />
      <add key="BrightstarDB.StoreLocation" value="d:\brightstar\" />


      <!-- Cache on a different disk from the B* stores to maximize disk throughput.
           Disk cache will be written to the directory e:\bscache -->
      <add key="BrightstarDB.QueryCacheDirectory" value="e:\bscache\"/>


      <!-- Allow disk cache to grow to up to 200GB in size -->
      <add key="BrightstarDB.QueryCacheDiskSpace" value="204800" /> 
    </appSettings>
  </configuration>


This sample has no disk cache because there is no valid location for the cache to be created::

  <configuration>
    <appSettings>
      <add key="BrightstarDB.EnableQueryCache" value="true" />
      <!-- 1GB in-memory cache -->
      <add key="BrightstarDB.QueryCacheMemory" value=1024"/>


      <!-- This property is not used because there is no 
            BrightstarDB.QueryCacheDirectory or
            BrightstarDB.StoreLocation setting defined. -->
      <add key="BrightstarDB.QueryCacheDiskSpace" value="204800" /> 


    </appSettings>
  </configuration>

  
  
.. _Logging:

*********************
 Configuring Logging
*********************


.. _TraceSource: http://msdn.microsoft.com/en-us/library/system.diagnostics.tracesource.aspx


BrightstarDB uses the .NET diagnostics infrastructure for logging. This provides a good deal 
of runtime flexibility over what messages are logged and how/where they are logged. All 
logging performed by BrightstarDB is written to a `TraceSource`_ named "BrightstarDB". 

The default configuration for this trace source depends on whether or not the 
`BrightstarDB.StoreLocation` configuration setting is provided in the application configuration 
file. If this setting is provided then the BrightstarDB trace source will be automatically 
configured to write to a log.txt file contained in the directory specified as the store location.
By default the trace source is set to log Information level messages and above.

Other logging options can be configured by entries in the <system.diagnostics> section of the 
application configuration file.

To log all messages (including debug messages), you can modify the TraceSource's `switchLevel`
as follows::

  <system.diagnostics>
    <sources>
      <source name="BrightstarDB" switchValue="Verbose"/>
    </sources>
  </system.diagnostics>

Equally you can use other switchValue settings to reduce the amount of logging performed by 
BrightstarDB.


.. _Preloading_Stores:

******************
 Preloading Stores
******************

The BrightstarDB server can be configured to automatically preload the active pages from one
or more stores into the in-memory page-cache. Preloading the pages trades-off a slightly longer 
server start-up time for a reduced time to respond to the first incoming request. By default
preloading is disabled and pages will be pulled into the cache on an as-needed basis.

Configuring Basic Preloading
============================

As preloading is concerned with populating the BrightstarDB store page cache, it can only be
enabled on a BrightstarDB server that is using an embedded connection to a store directory.
Basic preloading will fill the cache with pages from all stores in the store directory in
an equal ratio, so if there are 10 stores in the directory, each will be allowed to use
up to 10% of the available cache. Basic preloading proceeds in order of store size
(from smallest to largest store based on their data file sizes), so if smaller stores
do not use up their full allocation of pages, the remaining space can be shared amongst
the remaining larger stores as they are pre-loaded. 

To enable basic preloading, the following needs to be added to the ``brightstar``
element in the server application (or web) configuration file::

  <preloadPages enabled="true" />

Advanced Preloading
===================

Basic preloading is a simple strategy that makes the assumption that all stores in a directory
are equally important - each is preloaded to the same extent. In some cases as an administrator
you may want to prioritize some stores over others. 

To allow for this you can assign one or more stores a cache ratio number. This number specifies the 
relative amount of page cache space to be assigned to the store, so a store with a cache ratio of 3 
gets 3x the pages that a store with a cache ratio of 1 is assigned, and 1.5x the pages that a store 
with a cache ratio of 2. By default all stores have a cache ratio of 1 assigned, but you can also
set this default to 0.

To configure advanced preloading you add a ``store`` element child to the ``preloadPages`` element
as shown here::

    <preloadPages enabled="true">
        <store name="storeA" cacheRatio="4" />
        <store name="storeB" cacheRatio="2" />
    </preloadPages>

To understand how cache ratios work, imagine that the server using this configuration is actually
serving 4 stores, storeA, storeB, storeC and storeD, and that the server is configured with a 
page cache size of 2048M As the default cache ratio for a store is 1, the effective ratios for 
the stores are:

========== ==============
Store Name Cache Ratio
========== ==============
storeA     4
storeB     2
storeC     1
storeD     1
========== ==============

The sum of those ratios is (4+2+1+1) = 8. So storeC and storeD are assigned one-eighth of the
page cache, storeB is assigned one-quarter and storeA one-half, making the assigned page cache
preload sizes:

========== ============== =================
Store Name Cache Ratio    Preload Size
========== ============== =================
storeA     4              1024M
storeB     2              512M
storeC     1              256M
storeD     1              256M
========== ============== =================

It is also possible to change the default cache ratio assigned to stores that are not explicitly
configured by adding a ``defaultCacheRatio`` attribute to the ``preloadPages`` element::

    <preloadPages enabled="true" defaultCacheRatio="2">
        <store name="storeA" cacheRatio="4" />
        <store name="storeB" cacheRatio="2" />
    </preloadPages>
    
The configuration above changes the cache preload sizes for the stores as follows:

========== ============== =================
Store Name Cache Ratio    Preload Size
========== ============== =================
storeA     4              819.2M
storeB     2              409.6M
storeC     2              409.6M
storeD     2              409.6M
========== ============== =================

It is also possible to use the ``defaultCacheRatio`` to disable preloading for stores
that are not explicitly named, by setting the default ratio to zero::

    <preloadPages enabled="true" defaultCacheRatio="0">
        <store name="storeA" cacheRatio="4" />
        <store name="storeB" cacheRatio="2" />
    </preloadPages>

This leads the the following preloaded cache sizes:

========== ============== =================
Store Name Cache Ratio    Preload Size
========== ============== =================
storeA     4              1365.3M
storeB     2              682.7M
storeC     0              0M
storeD     0              0M
========== ============== =================

.. _Controlling_Transaction_Logging:

********************
 Transaction Logging
********************

BrightstarDB provides a persistent text log of the transactions applied to a store. This log is contained in the file
``transactions.bs`` and is indexed by the file ``transactionheaders.bs``. The purpose of these files is to enable a 
transaction or set of transactions to be replayed at any time either against the same store or against another 
store as a form of data synchronization. The BrightstarDB API provides methods for accessing the index; retrieving
the data for specific transactions from the log files; and replaying transactions.

Disabling Transaction Logging
=============================

The ``transaction.bs`` file lists the RDF quads inserted and deleted by
each transaction executed against the store, and so over time this file can grow to be quite large. For this
reason, from release 1.9 of BrightstarDB it is now possible to control whether a store logs these transactions 
or not and it is possible for a BrightstarDB server (or embedded application) to control the default setting
for this configuration.

Disabling Store Logging
-----------------------

Transaction logging for an individual store is controlled by the existence of the ``transactionheaders.bs`` file
in the directory for the store. If this file exists when a job is processed, then the data for that job will be logged
to the ``transactions.bs`` file and an index entry appended to the ``transactionheaders.bs`` file. If the file does not 
exist when a job is processed, then no data will be logged for that job.

This makes it easy to disable logging on a store - simply delete (or rename) the ``transactionheaders.bs`` and ``transactions.bs``
files from the store's directory. In either case it is recommended to delete or rename the ``transactionheaders.bs`` file 
first.

Equally it is easy to enable logging on a store - simply create an empty file named ``transactionheaders.bs`` in the
store's directory. The ``transactions.bs`` file will be automatically created if it does not exist (if it does exist,
new transaction data will be logged to the end of the existing file).

Specifying the Server Default
-----------------------------

For regular Windows/Mono applications or web applications (i.e. those applications that can read from an ``app.config`` or
``web.config`` file), the default transaction logging configuration can be specified in the ``brightstar`` configuration section::

  <?xml version="1.0"?>
  <configuration>
    <configSections>
      <section name="brightstar" type="BrightstarDB.Config.BrightstarConfigurationSectionHandler, BrightstarDB" />
    </configSections>

    <appSettings>

        <!-- Other server configuration options can be specified here -->
    
    <brightstar>
    
      <!-- Disable transaction logging -->
      <transactionLogging enabled="false" />
    
    </brightstar>
    
  </configuration>
  

Alternatively (and for those platforms where there is no support for ``app.config files``), the configuration can be specified
programatically when creating the client by creating an instance of ``BrightstarDB.Config.EmbeddedServiceConfiguration`` and
passing that as the optional second parameter to the ``BrightstarService.GetClient()`` method::

    var client = BrightstarService.GetClient(myConnectionString,
        new EmbeddedServiceConfiguration(enableTransactionLoggingOnNewStores: false));

Note: These options merely set the default logging setting for newly created stores. In effect we are controlling whether or
not the `transactionheaders.bs` file is created when the store is first created. Logging for an individual store can still
be enabled or disabled by managing the `transactionheaders.bs` file as described in the section above.