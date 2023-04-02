## Build Status

| AppVeyor | Visual Studio Online |
| --- | --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/1i2k4gx5gfmmtyju?svg=true)](https://ci.appveyor.com/project/emgarten/nuget-catalogreader) | [![VSO](https://hackamore.visualstudio.com/_apis/public/build/definitions/abbff132-0981-4267-a80d-a6e7682a75a9/4/badge)](https://github.com/emgarten/nuget.catalogreader) |

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

## CI builds

CI builds are located on the following NuGet feed:

``https://nuget.blob.core.windows.net/packages/index.json``

The list of packages on this feed is [here](https://nuget.blob.core.windows.net/packages/sleet.packageindex.json).

## Contributing

We welcome contributions. If you are interested in contributing you can report an issue or open a pull request to propose a change.

### License
[MIT License](https://raw.githubusercontent.com/emgarten/NuGet.CatalogReader/main/LICENSE)