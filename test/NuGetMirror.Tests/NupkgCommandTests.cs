using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using NuGet.Versioning;
using Sleet;
using Test.Common;
using Xunit;

namespace NuGetMirror.Tests
{
    public class NupkgCommandTests
    {
        [Fact]
        public async Task VerifyPackagesAreDownloadedInV2Structure()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var beforeDate = DateTimeOffset.UtcNow;
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, "--folder-format", "v2", feedUri.AbsoluteUri, "--delay", "0" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV2(nupkgsOutFolder, catalogLog).ToList();

                results.Select(e => e.Identity).ShouldBeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

                var afterDate = DateTimeOffset.UtcNow;
                var cursor = MirrorUtility.LoadCursor(new DirectoryInfo(nupkgsOutFolder));

                (cursor <= afterDate && cursor >= beforeDate).Should().BeTrue("the cursor should match the catalog");

                var errorLog = Path.Combine(nupkgsOutFolder, "lastRunErrors.txt");
                File.Exists(errorLog).Should().BeFalse();
            }
        }

        [Fact]
        public async Task VerifyPackagesAreDownloadedInV3Structure()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var beforeDate = DateTimeOffset.UtcNow;
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, feedUri.AbsoluteUri, "--delay", "0" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog).ToList();

                results.Select(e => e.Identity).ShouldBeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

                var afterDate = DateTimeOffset.UtcNow;
                var cursor = MirrorUtility.LoadCursor(new DirectoryInfo(nupkgsOutFolder));

                (cursor <= afterDate && cursor >= beforeDate).Should().BeTrue("the cursor should match the catalog");

                var errorLog = Path.Combine(nupkgsOutFolder, "lastRunErrors.txt");
                File.Exists(errorLog).Should().BeFalse();
            }
        }

        [Fact]
        public async Task GivenMultiplePackagesVerifyIncludeTakesOnlyMatches()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var beforeDate = DateTimeOffset.UtcNow;
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                var packageA = new TestNupkg("aa", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                var packageB = new TestNupkg("ab", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageB);

                var packageC = new TestNupkg("c", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageC);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, feedUri.AbsoluteUri, "--delay", "0", "-i", "a*" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog).ToList();

                results.Select(e => e.Identity).ShouldBeEquivalentTo(
                    new[] {
                        new PackageIdentity("aa", NuGetVersion.Parse("1.0.0")),
                        new PackageIdentity("ab", NuGetVersion.Parse("1.0.0"))
                    });
            }
        }

        [Fact]
        public async Task GivenMultiplePackagesVerifyExcludeRemovesC()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var beforeDate = DateTimeOffset.UtcNow;
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                var packageA = new TestNupkg("aa", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                var packageB = new TestNupkg("ab", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageB);

                var packageC = new TestNupkg("c", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageC);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, feedUri.AbsoluteUri, "--delay", "0", "-e", "a*" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog).ToList();

                results.Select(e => e.Identity).ShouldBeEquivalentTo(
                    new[] {
                        new PackageIdentity("c", NuGetVersion.Parse("1.0.0"))
                    });
            }
        }

        [Fact]
        public async Task GivenALargeNumberOfPackagesVerifyAllAreDownloaded()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                var expected = new HashSet<PackageIdentity>();

                for (var i = 0; i < 200; i++)
                {
                    var identity = new PackageIdentity(Guid.NewGuid().ToString(), NuGetVersion.Parse($"{i}.0.0"));

                    if (expected.Add(identity))
                    {
                        var package = new TestNupkg(identity.Id, identity.Version.ToNormalizedString());
                        TestNupkg.Save(nupkgsFolder, package);
                    }
                }

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, feedUri.AbsoluteUri, "--delay", "0" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog).ToList();

                results.Select(e => e.Identity).ShouldBeEquivalentTo(expected);

                var errorLog = Path.Combine(nupkgsOutFolder, "lastRunErrors.txt");
                File.Exists(errorLog).Should().BeFalse();
            }
        }
    }
}
