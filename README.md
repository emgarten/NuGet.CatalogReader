## Build Status

| Github |
| --- |
| [![.NET test](https://github.com/emgarten/NuGet.CatalogReader/actions/workflows/dotnet.yml/badge.svg)](https://github.com/emgarten/NuGet.CatalogReader/actions/workflows/dotnet.yml) |

# What are these tools?

NuGetMirror.exe is a command line tool to mirror nuget.org to disk, or any NuGet v3 feed. It supports filtering package ids and wildcards to narrow down the set of mirrored packages.

NuGet.CatalogReader is a library for reading package ids, versions, and the change history of a NuGet v3 feeds or nuget.org.

## Getting NuGetMirror

### Install dotnet global tool
1. `dotnet tool install -g nugetmirror`
1. `nugetmirror` should now be on your *PATH*

# Quick start

## Using NuGetMirror

Mirror all packages to a folder on disk.

``NuGetMirror nupkgs https://api.nuget.org/v3/index.json -o d:\tmp``

NuGetMirror can be used to continually sync the latest packages. Runs store to the last commit time to disk, future runs will resume from this point and only get new or updated packages.

### Auth support

NuGetMirror can use credentials from a *nuget.config* file. Pass the name of the source instead of the index.json URI and ensure that the config is in the working directory or one of the common nuget.config locations.

## Using NuGet.CatalogReader

Discover all packages in a feed using ``GetFlattenedEntriesAsync``. To see the complete history including edits use ``GetEntriesAsync``.

```csharp
var feed = new Uri("https://api.nuget.org/v3/index.json");

using (var catalog = new CatalogReader(feed))
{
    foreach (var entry in await catalog.GetFlattenedEntriesAsync())
    {
        Console.WriteLine($"[{entry.CommitTimeStamp}] {entry.Id} {entry.Version}");
        await entry.DownloadNupkgAsync(@"d:\output");
    }
}
```

NuGet v3 feeds that do not have a catalog can be read using `FeedReader`.

```csharp
var feed = new Uri("https://api.nuget.org/v3/index.json");

using (var feedReader = new FeedReader(feed))
{
    foreach (var entry in await feedReader.GetPackagesById("NuGet.Versioning"))
    {
        Console.WriteLine($"{entry.Id} {entry.Version}");
        await entry.DownloadNupkgAsync(@"d:\output");
    }
}
```

## Contributing

We welcome contributions. If you are interested in contributing you can report an issue or open a pull request to propose a change.

### License
[MIT License](https://raw.githubusercontent.com/emgarten/NuGet.CatalogReader/main/LICENSE)
