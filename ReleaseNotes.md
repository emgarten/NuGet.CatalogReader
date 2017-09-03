# Release Notes

## 1.3.0
* ``--additional-output`` support for desktop versions of NuGet.Mirror.exe. This allows using multiple drives for mirroring a feed.
* NuGetMirror can be used as a DotNetCliToolReference with dotnet CLI.
* NuGetMirror contains exposes a NuGetMirror property for finding the path to NuGetMirror.exe in msbuild.
* Moved to netstandard2.0/netcoreapp2.0
* Updated to NuGet 4.3.0 libraries