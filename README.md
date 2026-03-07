# NuGet.CatalogReader

Read NuGet v3 feed catalogs, mirror packages to disk, and query package metadata.

| | NuGet.CatalogReader | NuGetMirror |
| --- | --- | --- |
| **Package** | [![NuGet](https://img.shields.io/nuget/v/NuGet.CatalogReader.svg)](https://www.nuget.org/packages/NuGet.CatalogReader/) | [![NuGet](https://img.shields.io/nuget/v/NuGetMirror.svg)](https://www.nuget.org/packages/NuGetMirror/) |
| **Downloads** | [![NuGet Downloads](https://img.shields.io/nuget/dt/NuGet.CatalogReader.svg)](https://www.nuget.org/packages/NuGet.CatalogReader/) | [![NuGet Downloads](https://img.shields.io/nuget/dt/NuGetMirror.svg)](https://www.nuget.org/packages/NuGetMirror/) |

[![.NET test](https://github.com/emgarten/NuGet.CatalogReader/actions/workflows/dotnet.yml/badge.svg)](https://github.com/emgarten/NuGet.CatalogReader/actions/workflows/dotnet.yml)

## Table of contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [NuGetMirror CLI tool](#nugetmirror-cli-tool)
  - [Mirror packages (nupkgs)](#mirror-packages-nupkgs)
  - [List packages (list)](#list-packages-list)
  - [Auth support](#auth-support)
- [NuGet.CatalogReader library](#nugetcatalogreader-library)
  - [Reading catalog entries](#reading-catalog-entries)
  - [Time range filtering](#time-range-filtering)
  - [Reading feeds without a catalog](#reading-feeds-without-a-catalog)
  - [Download modes](#download-modes)
  - [API overview](#api-overview)
- [Building from source](#building-from-source)
- [Contributing](#contributing)
- [License](#license)

## Overview

This repository contains two packages:

- **NuGet.CatalogReader** — A library for reading package ids, versions, and the change history of NuGet v3 feeds or nuget.org.
- **NuGetMirror** — A command line tool to mirror nuget.org (or any NuGet v3 feed) to disk. Supports filtering by package id and wildcards.

## Prerequisites

- .NET 8.0, 9.0, or 10.0 SDK

## Installation

### NuGet.CatalogReader library

```
dotnet add package NuGet.CatalogReader
```

### NuGetMirror CLI tool

```
dotnet tool install -g nugetmirror
```

After installation, `nugetmirror` will be available on your PATH.

## NuGetMirror CLI tool

### Mirror packages (nupkgs)

Mirror all packages to a folder on disk:

```
nugetmirror nupkgs https://api.nuget.org/v3/index.json -o /tmp/packages
```

NuGetMirror stores the last commit time to disk so that future runs resume from that point and only download new or updated packages.

#### Options

| Option | Description |
| --- | --- |
| `-o\|--output` | Output directory for nupkgs (required) |
| `--folder-format` | Output folder format: `v2` or `v3` (default: `v3`) |
| `-i\|--include-id` | Include only matching package ids (supports wildcards, can be repeated) |
| `-e\|--exclude-id` | Exclude matching package ids (supports wildcards, can be repeated) |
| `--latest-only` | Mirror only the latest version of each package |
| `--stable-only` | Exclude pre-release packages |
| `--start` | Beginning of commit time range (exclusive) |
| `--end` | End of commit time range (inclusive) |
| `--max-threads` | Maximum concurrent downloads (default: 8) |
| `--delay` | Delay in minutes before downloading the latest packages (default: 10) |
| `--additional-output` | Additional output directory for load balancing across drives |
| `--ignore-errors` | Continue on download errors |

#### Examples

Mirror only packages matching a wildcard:

```
nugetmirror nupkgs https://api.nuget.org/v3/index.json -o /tmp/packages -i "Newtonsoft.*"
```

Mirror stable-only, latest versions:

```
nugetmirror nupkgs https://api.nuget.org/v3/index.json -o /tmp/packages --latest-only --stable-only
```

### List packages (list)

List all packages in a feed:

```
nugetmirror list https://api.nuget.org/v3/index.json
```

#### Options

| Option | Description |
| --- | --- |
| `-s\|--start` | Beginning of commit time range (exclusive) |
| `-e\|--end` | End of commit time range (inclusive) |
| `-v\|--verbose` | Write additional network call information |

### Auth support

NuGetMirror can use credentials from a `nuget.config` file. Pass the source name instead of the index.json URI and ensure that the config is in the working directory or one of the [common nuget.config locations](https://learn.microsoft.com/nuget/consume-packages/configuring-nuget-behavior#config-file-locations-and-uses).

```
nugetmirror nupkgs MyPrivateFeed -o /tmp/packages
```

With a `nuget.config` like:

```xml
<packageSources>
  <add key="MyPrivateFeed" value="https://myfeed.example.com/v3/index.json" />
</packageSources>
<packageSourceCredentials>
  <MyPrivateFeed>
    <add key="Username" value="user" />
    <add key="ClearTextPassword" value="token" />
  </MyPrivateFeed>
</packageSourceCredentials>
```

## NuGet.CatalogReader library

### Reading catalog entries

Discover all packages in a feed using `GetFlattenedEntriesAsync`. To see the complete history including edits, use `GetEntriesAsync`.

```csharp
var feed = new Uri("https://api.nuget.org/v3/index.json");

using (var catalog = new CatalogReader(feed))
{
    foreach (var entry in await catalog.GetFlattenedEntriesAsync())
    {
        Console.WriteLine($"[{entry.CommitTimeStamp}] {entry.Id} {entry.Version}");
    }
}
```

### Time range filtering

Retrieve entries within a specific time range:

```csharp
var feed = new Uri("https://api.nuget.org/v3/index.json");

using (var catalog = new CatalogReader(feed))
{
    var start = DateTimeOffset.UtcNow.AddHours(-1);
    var end = DateTimeOffset.UtcNow;

    foreach (var entry in await catalog.GetEntriesAsync(start, end, CancellationToken.None))
    {
        Console.WriteLine($"[{entry.CommitTimeStamp}] {entry.Id} {entry.Version}");
    }
}
```

### Reading feeds without a catalog

NuGet v3 feeds that do not have a catalog can be read using `FeedReader`:

```csharp
var feed = new Uri("https://api.nuget.org/v3/index.json");

using (var feedReader = new FeedReader(feed))
{
    foreach (var entry in await feedReader.GetPackagesById("NuGet.Versioning"))
    {
        Console.WriteLine($"{entry.Id} {entry.Version}");
        await entry.DownloadNupkgAsync("/tmp/output");
    }
}
```

### Download modes

When downloading packages, specify how to handle existing files with `DownloadMode`:

| Mode | Behavior |
| --- | --- |
| `FailIfExists` | Throw if the file already exists |
| `SkipIfExists` | Skip the download if the file already exists |
| `OverwriteIfNewer` | Overwrite only if the new package is newer |
| `Force` | Always overwrite |

```csharp
await entry.DownloadNupkgAsync("/tmp/output", DownloadMode.SkipIfExists, CancellationToken.None);
```

### API overview

| Type | Description |
| --- | --- |
| `CatalogReader` | Reads catalog entries from a NuGet v3 feed |
| `FeedReader` | Reads packages from a NuGet v3 feed without requiring a catalog |
| `CatalogEntry` | A catalog page entry with commit metadata (`CommitTimeStamp`, `IsAddOrUpdate`, `IsDelete`) |
| `PackageEntry` | A package with download and metadata methods (`DownloadNupkgAsync`, `GetNuspecAsync`, `IsListedAsync`) |
| `CatalogPageEntry` | Represents a page in the catalog index |
| `DownloadMode` | Controls file overwrite behavior when downloading |

## Building from source

Clone the repository and run the build script for your platform:

```bash
# macOS / Linux
./build.sh

# Windows
./build.ps1
```

The build script will install the required .NET SDKs locally, restore packages, build, pack, and run tests.

## Contributing

We welcome contributions. If you are interested in contributing you can report an issue or open a pull request to propose a change.

## License

[MIT License](https://raw.githubusercontent.com/emgarten/NuGet.CatalogReader/main/LICENSE)
