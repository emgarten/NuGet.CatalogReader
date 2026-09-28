using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Linq;
using AwesomeAssertions;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Packaging;
using NuGet.Protocol;
using NuGet.Test.Helpers;
using Test.Common;
using Xunit;

namespace NuGetMirror.CliTool.Tests
{
    public class BasicTests
    {
        /// <summary>
        /// Dotnet install nugetmirror from the nupkg created by build.ps1 or build.sh
        /// and run it to verify the tool is working
        /// </summary>
        [Fact]
        public async Task RunToolVerifySuccess()
        {
            using (var testContext = new TestFolder())
            {
                var dir = Path.Combine(testContext.Root, "tooloutput");
                Directory.CreateDirectory(dir);

                var dotnetExe = GetDotnetPath();
                var nupkgsFolder = CmdRunner.GetPath("artifacts/nupkgs");

                var packages = LocalFolderUtility.GetPackagesV2(nupkgsFolder, "NuGetMirror", NullLogger.Instance, TestContext.Current.CancellationToken).ToList();

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

                var dllPath = Path.Combine(dir, ".store", "nugetmirror", version, "nugetmirror", version, "tools", "net8.0", "any", "NuGetMirror.dll");

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
            // Use the dotnet install running the tests: <root>/shared/Microsoft.NETCore.App/<version>/
            var dotnetRoot = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

            return Path.Combine(dotnetRoot, RuntimeEnvironmentHelper.IsWindows ? "dotnet.exe" : "dotnet");
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
