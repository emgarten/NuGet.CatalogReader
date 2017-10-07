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
                var dir = Path.Combine(testContext.Root, "project");
                Directory.CreateDirectory(dir);

                var dotnetExe = GetDotnetPath();
                var exeFile = new FileInfo(dotnetExe);
                var nupkgsFolder = Path.Combine(exeFile.Directory.Parent.FullName, "artifacts", "nupkgs");

                var nupkg = LocalFolderUtility.GetPackagesV2(nupkgsFolder, "NuGetMirror", NullLogger.Instance)
                    .OrderByDescending(e => e.Nuspec.GetVersion())
                    .First();

                var nupkgVersion = nupkg.Nuspec.GetVersion().ToNormalizedString();

                var result = await CmdRunner.RunAsync(dotnetExe, dir, "new classlib");
                result.Success.Should().BeTrue();

                var projectPath = Path.Combine(dir, "project.csproj");

                var pathContext = NuGetPathContext.Create(dir);
                var pathResolver = new FallbackPackagePathResolver(pathContext);

                // Delete restore assets file
                var toolInstallPath = Path.Combine(pathContext.UserPackageFolder, ".tools", "nugetmirror");
                Delete(new DirectoryInfo(toolInstallPath));

                // Delete the tool package itself if it exists
                var toolPackagePath = Path.Combine(pathContext.UserPackageFolder, "nugetmirror", nupkgVersion);
                Delete(new DirectoryInfo(toolPackagePath));

                // Add a reference to the tool
                var xml = XDocument.Load(projectPath);
                xml.Root.Add(new XElement(XName.Get("ItemGroup"),
                    new XElement(XName.Get("DotNetCliToolReference"),
                    new XAttribute("Include", "NuGetMirror"),
                    new XAttribute("Version", nupkgVersion))));
                xml.Save(projectPath);

                // Restore the tool
                result = await CmdRunner.RunAsync(dotnetExe, dir, $"restore --source {nupkgsFolder}");
                result.Success.Should().BeTrue();

                // Run the tool
                result = await CmdRunner.RunAsync(dotnetExe, dir, $"nugetmirror --version");
                result.Success.Should().BeTrue();
                result.Errors.Should().BeNullOrEmpty();
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
