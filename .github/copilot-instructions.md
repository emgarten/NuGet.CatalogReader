# Copilot Instructions for NuGet.CatalogReader

## Project Overview

This repository contains two main components:

- **NuGet.CatalogReader** (`src/NuGet.CatalogReader/`): A .NET library (`netstandard2.0`) for reading package metadata and change history from NuGet v3 feeds.
- **NuGetMirror** (`src/NuGetMirror/`): A .NET CLI tool (`net6.0`, `net8.0`, `net9.0`) that mirrors NuGet feeds to disk, supporting incremental syncs and package ID filtering.
- **NuGet.CatalogValidator** (`src/NuGet.CatalogValidator/`): A validator tool for catalog integrity.

## Building

Build scripts wrap MSBuild and handle restore, build, test, and pack steps.

**Windows:**
```powershell
./build.ps1                      # Release build with tests and pack
./build.ps1 -Configuration Debug # Debug build
./build.ps1 -SkipTests           # Skip tests
./build.ps1 -SkipPack            # Skip NuGet pack
```

**Linux/macOS:**
```bash
./build.sh
```

Build artifacts are placed in `artifacts/nupkgs/` (NuGet packages) and `artifacts/publish/` (executables).

## Running Tests

Tests use xUnit. Run individual test projects with:

```bash
dotnet test test/NuGet.CatalogReader.Tests/NuGet.CatalogReader.Tests.csproj
dotnet test test/NuGetMirror.Tests/NuGetMirror.Tests.csproj
dotnet test test/NuGetMirror.CliTool.Tests/NuGetMirror.CliTool.Tests.csproj
```

Or run all tests via the build script (default behavior).

## Code Conventions

- **Language**: C# with standard .NET coding conventions
- **Async**: Use `async`/`await` throughout; async methods are suffixed with `Async`
- **Nullability**: Follow existing patterns in each file
- **Logging**: Use `ILogger` from `NuGet.Common` in library code; `ConsoleLogger`/`FileLogger` in the CLI tool
- **HTTP**: Use `HttpSource` from NuGet.Protocol for HTTP access in the library
- **Error handling**: Prefer exceptions with descriptive messages; use `CancellationToken` parameters for async operations

## Project Structure

```
src/
  NuGet.CatalogReader/        # Library: CatalogReader, CatalogEntry, FeedReader, etc.
  NuGetMirror/                # CLI tool: commands, logging, mirroring logic
  NuGet.CatalogValidator/     # Catalog validation tool
test/
  NuGet.CatalogReader.Tests/  # Tests for the library
  NuGetMirror.Tests/          # Tests for mirror functionality
  NuGetMirror.CliTool.Tests/  # CLI integration tests
  Test.Common/                # Shared test utilities and fixtures
build/                        # MSBuild scripts and shared build configuration
```

## Key APIs (NuGet.CatalogReader library)

- `CatalogReader` – Main class for reading NuGet v3 catalog feeds
  - `GetFlattenedEntriesAsync()` – Latest version of each package
  - `GetEntriesAsync()` – Full change history including edits
  - `GetPackagesById()` – Entries for a specific package ID
- `FeedReader` – For v3 feeds without a catalog
- `CatalogEntry` – Represents a single catalog entry with metadata and download support

## CI/CD

- **dotnet.yml**: Runs on push to `main` and on PRs; builds and tests on Linux (`ubuntu-latest`) and Windows (`windows-latest`) using .NET 8.0.x.
- **publish.yml**: Manual dispatch workflow; publishes NuGet packages to nuget.org using the `NUGET_API_KEY` secret.
