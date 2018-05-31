using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using FluentAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Test.Helpers;
using Test.Common;

namespace NuGetMirror.CliTool.Tests
{
    public class BasicTests
    {
        /// <summary>
        /// Add a DotNetCliToolReference to NuGetMirror
        /// Restore the project
        /// Run dotnet nugetmirror to verify the tool is working
        /// Currently the nupkg is only produced on Windows,
        /// for that reason this test only runs on windows.
        /// </summary>
        [WindowsFact]
        public async Task RunToolVerifySuccess()
        {
            using (var testContext = new TestFolder())
            {
                var dir = Path.Combine(testContext.Root, "tooloutput");
                Directory.CreateDirectory(dir);

                var dotnetExe = GetDotnetPath();
                var exeFile = new FileInfo(dotnetExe);
                var nupkgsFolder = Path.Combine(exeFile.Directory.Parent.FullName, "artifacts", "nupkgs");

                var packages = LocalFolderUtility.GetPackagesV2(nupkgsFolder, "NuGetMirror", NullLogger.Instance).ToList();

                if (packages.Count < 1)
                {
                    throw new Exception("Run build.ps1 first to create the nupkgs.");
                }

                var nupkg = packages
                    .OrderByDescending(e => e.Nuspec.GetVersion())
                    .First();

                var version = nupkg.Nuspec.GetVersion().ToNormalizedString();

                var result = await CmdRunner.RunAsync(dotnetExe, testContext.Root, $"tool install nugetmirror --version {version} --add-source {nupkgsFolder} --tool-path {dir}");
                result.Success.Should().BeTrue(result.AllOutput);

                var dllPath = Path.Combine(dir, ".store", "nugetmirror", version, "nugetmirror", version, "tools", "netcoreapp2.1", "any", "NuGetMirror.dll");

                if (!File.Exists(dllPath))
                {
                    throw new Exception("Tool did not install to the expected location: " + dllPath);
                }

                // Run the tool
                result = await CmdRunner.RunAsync(dotnetExe, dir, $"{dllPath} --version");
                result.Success.Should().BeTrue(result.Errors);
                result.Errors.Should().BeNullOrEmpty(result.Errors);
            }
        }

        private static string GetDotnetPath()
        {
            var dotnetExeRelativePath = ".cli/dotnet.exe";

            if (!RuntimeEnvironmentHelper.IsWindows)
            {
                dotnetExeRelativePath = ".cli/dotnet";
            }

            return CmdRunner.GetPath(dotnetExeRelativePath);
        }

        private static void Delete(DirectoryInfo dir)
        {
            if (!dir.Exists)
            {
                return;
            }

            try
            {
                foreach (var subDir in dir.EnumerateDirectories())
                {

                    Delete(subDir);
                }

                dir.Delete(true);
            }
            catch
            {
                // Ignore exceptions
            }
        }
    }
}
