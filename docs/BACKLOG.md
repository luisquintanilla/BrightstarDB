# BrightstarDB Backlog

Items deferred from code review of [PR #1](https://github.com/luisquintanilla/BrightstarDB/pull/1). Each item is self-contained and shovel-ready.

---

## 1. Wire Configuration-Driven Permission Providers to ASP.NET Core DI

**Source:** PR #1 review thread on `Program.cs` line 47
**Priority:** Medium
**Effort:** ~2-4 hours

### Problem

`Program.cs` registers fallback permission providers that grant `StorePermissions.All` and `SystemPermissions.All` to any authenticated user:

```csharp
// Program.cs lines 44-47
builder.Services.AddSingleton<AbstractStorePermissionsProvider>(
    new FallbackStorePermissionsProvider(StorePermissions.All));
builder.Services.AddSingleton<AbstractSystemPermissionsProvider>(
    new FallbackSystemPermissionsProvider(SystemPermissions.All));
```

Meanwhile, `BrightstarServiceConfiguration` (in `src/core/BrightstarDB.Server.AspNetCore/Configuration/BrightstarServiceConfiguration.cs`) already defines `StorePermissions` and `SystemPermissions` sections with `Authenticated` and `Anonymous` permission levels — but `Program.cs` never reads them.

The old Nancy server (`BrightstarServiceConfigurationSectionHandler`) supported configuration-driven permission providers: `<fallback>`, `<static>`, and `<combine>` elements that mapped to `FallbackStorePermissionsProvider`, `StaticStorePermissionsProvider`, and `CombiningStorePermissionsProvider` respectively.

### What to Do

1. **Read the existing config** in `Program.cs` after binding `BrightstarServiceConfiguration`:
   ```csharp
   var config = builder.Configuration
       .GetSection(BrightstarServiceConfiguration.SectionName)
       .Get<BrightstarServiceConfiguration>();
   ```

2. **Register providers based on config** instead of hardcoding:
   - If `config?.StorePermissions` is set → build a `FallbackStorePermissionsProvider` with `config.StorePermissions.Authenticated` and `config.StorePermissions.Anonymous`
   - If `config?.SystemPermissions` is set → same for system permissions
   - If no config → keep current fallback to `StorePermissions.All` / `SystemPermissions.All` (preserves backward compatibility)

3. **Optionally** support static user-based permissions (via `StaticStorePermissionsProvider` / `StaticSystemPermissionsProvider`) and combining providers, matching the old Nancy config model.

### Files to Modify

- `src/core/BrightstarDB.Server.AspNetCore/Program.cs` — replace lines 43-47 with config-driven registration
- `src/core/BrightstarDB.Server.AspNetCore/Configuration/BrightstarServiceConfiguration.cs` — verify `StorePermissions` and `SystemPermissions` config classes have all needed properties

### Files to Reference

- `src/core/BrightstarDB.Server.AspNetCore/Authorization/FallbackStorePermissionsProvider.cs` — constructor takes `StorePermissions authenticated` and optional `StorePermissions anonymous`
- `src/core/BrightstarDB.Server.AspNetCore/Authorization/FallbackSystemPermissionsProvider.cs` — same pattern
- `src/core/BrightstarDB.Server.AspNetCore/Authorization/StaticStorePermissionsProvider.cs` — takes user→permission map
- `src/core/BrightstarDB.Server.AspNetCore/Authorization/CombiningStorePermissionsProvider.cs` — unions two providers
- `src/core/BrightstarDB.Server.Modules/BrightstarServiceConfigurationSectionHandler.cs` — old Nancy config parser (reference for config mapping)

### How to Test

- Run existing tests: `dotnet test src\core\core.sln --no-build`
- Add a test in `BrightstarDB.Server.AspNetCore.Tests` that creates a `WebApplicationFactory<Program>` with custom `appsettings.json` containing permission config, then verifies that unauthenticated requests get 401 and authenticated requests with limited permissions get 403 on restricted endpoints.

### Example Config (`appsettings.json`)

```json
{
  "BrightstarService": {
    "ConnectionString": "type=embedded;storesDirectory=data",
    "Authentication": {
      "Realm": "BrightstarDB",
      "Credentials": [
        { "User": "admin", "Password": "secret", "Claims": ["admin"] },
        { "User": "reader", "Password": "readonly", "Claims": ["reader"] }
      ]
    },
    "StorePermissions": {
      "Authenticated": "Read,Export",
      "Anonymous": "None"
    },
    "SystemPermissions": {
      "Authenticated": "ListStores",
      "Anonymous": "None"
    }
  }
}
```

---

## 2. Harden AppVeyor CI dotnet-install PATH/DOTNET_ROOT

**Source:** PR #1 review thread on `appveyor.yml` line 8
**Priority:** Low
**Effort:** ~30 minutes

### Problem

The `appveyor.yml` install step downloads and runs `dotnet-install.ps1` but doesn't explicitly set `DOTNET_ROOT` or ensure the installed SDK location is on PATH for subsequent build steps:

```yaml
install:
  - ps: |
      Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'
      ./dotnet-install.ps1 -Channel 10.0 -Quality ga
```

The script prepends to `$env:PATH` in the current PowerShell process by default, but:
- If AppVeyor runs subsequent sections in separate processes, they won't see the updated PATH
- `DOTNET_ROOT` is not set, which some tools use to locate the SDK
- The install directory defaults to `$env:LOCALAPPDATA\Microsoft\dotnet` on Windows

### What to Do

Update the `install` section in `appveyor.yml` to:

```yaml
install:
  - choco install gitversion.portable -pre -y
  - ps: |
      # Install .NET 10 SDK
      $installDir = "$env:LOCALAPPDATA\Microsoft\dotnet"
      Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1'
      ./dotnet-install.ps1 -Channel 10.0 -Quality ga -InstallDir $installDir
      # Ensure PATH and DOTNET_ROOT are set for subsequent AppVeyor steps
      [Environment]::SetEnvironmentVariable("DOTNET_ROOT", $installDir, "Process")
      [Environment]::SetEnvironmentVariable("PATH", "$installDir;$env:PATH", "Process")
```

### Files to Modify

- `appveyor.yml` — update the `install` section

### How to Test

- Push to the branch and verify the AppVeyor build passes
- Check `dotnet --version` output in the `before_build` step shows the .NET 10 SDK version
