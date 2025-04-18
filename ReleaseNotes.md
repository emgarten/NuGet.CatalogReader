# Release Notes

## 3.3.2
* Fix for download nuspec helpers [PR](https://github.com/emgarten/NuGet.CatalogReader/pull/33)

## 3.3.0
* Add net9.0 support
* Update NuGet.* packages to 6.12.1
* NuGetMirror auth support using nuget.config [PR](https://github.com/emgarten/NuGet.CatalogReader/pull/30)

## 3.2.0
* Add net8.0 support, remove net7.0 support
* Update NuGet.* packages to 6.9.1

## 3.1.0
* Add net7.0 support

## 3.0.1
* Update NuGet.* packages to 6.2.1

## 3.0.0
* Updated to net6.0
* Updated dependencies

## 2.0.0
* Removed NuGetMirror.exe from nupkg, this should now be used as a dotnet tool only
* Updated to net5.0
* Removed Console project

## 1.6.0
* DotnetTool support for install -g
* Updated to netcoreapp2.1

## 1.5.0
* Update NuGet dependencies

## 1.4.0
* SemVer registration url support
* --latest-only option to retrieve the latest version of each package
* --start and --end for nupkgs command
* --stable-only option to filter out pre-release packages

## 1.3.0
* ``--additional-output`` support for desktop versions of NuGet.Mirror.exe. This allows using multiple drives for mirroring a feed.
* NuGetMirror can be used as a DotNetCliToolReference with dotnet CLI.
* NuGetMirror contains exposes a NuGetMirror property for finding the path to NuGetMirror.exe in msbuild.
* Moved to netstandard2.0/netcoreapp2.0
* Updated to NuGet 4.3.0 libraries
