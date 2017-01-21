# NuGet.CatalogReader

NuGet.CatalogReader is a library for reading the full list of package ids, versions, and the change history for NuGet v3 feeds.

NuGetMirror.exe is a tool that uses NuGet.CatalogReader to download packages from a NuGet v3 feed to a folder on disk.

| AppVeyor | Travis |
| --- | --- |
| [![AppVeyor](https://ci.appveyor.com/api/projects/status/1i2k4gx5gfmmtyju?svg=true)](https://ci.appveyor.com/project/emgarten/nuget-catalogreader) | [![Travis](https://travis-ci.org/emgarten/NuGet.CatalogReader.svg?branch=master)](https://travis-ci.org/emgarten/NuGet.CatalogReader) |

## Releases

* [Github releases](https://github.com/emgarten/NuGet.CatalogReader/releases/latest)
* [NuGet.CatalogReader on nuget.org](https://www.nuget.org/packages/NuGet.CatalogReader)
* [NuGetMirror on nuget.org](https://www.nuget.org/packages/NuGetMirror)

### Nightly build feed

 ``https://nuget.blob.core.windows.net/packages/index.json``
 
## Coding
This solution uses .NET Core, get the tools [here](http://dot.net/).

### License
[MIT License](https://raw.githubusercontent.com/emgarten/NuGet.CatalogReader/master/LICENSE)

## Using NuGet.CatalogReader

Discover all packages in a feed using ``GetFlattenedEntriesAsync``. To see the complete history including edits use ``GetEntriesAsync``.

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

## Using NuGetMirror.exe

Mirror all packages to a folder on disk.

``NuGetMirror.exe nupkgs https://api.nuget.org/v3/index.json -o d:\tmp``

