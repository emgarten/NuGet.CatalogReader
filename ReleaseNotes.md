# Release Notes

## 1.5.0

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