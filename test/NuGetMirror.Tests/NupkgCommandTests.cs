using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Common;
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

                var results = LocalFolderUtility.GetPackagesV2(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

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

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(new[] { new PackageIdentity("a", NuGetVersion.Parse("1.0.0")) });

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

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(
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

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(
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

                var errors = log.GetMessages(LogLevel.Error);
                exitCode.Should().Be(0, errors);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(expected);

                var errorLog = Path.Combine(nupkgsOutFolder, "lastRunErrors.txt");
                File.Exists(errorLog).Should().BeFalse();
            }
        }

        [Theory]
        [InlineData("v2")]
        [InlineData("v3")]
        public async Task VerifyUnchangedPackagesAreNotUpdatedWhenTheCatalogIsReadAgain(string folderFormat)
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var workingDir = new TestFolder())
            {
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                var updatedFilesPath = Path.Combine(nupkgsOutFolder, "updatedFiles.txt");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                TestNupkg.Save(nupkgsFolder, new TestNupkg("a", "1.0.0"));

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var args = new List<string> { "nupkgs", "-o", nupkgsOutFolder, "--folder-format", folderFormat, feedUri.AbsoluteUri, "--delay", "0" };
                var exitCode = await NuGetMirror.Program.MainCore(args.ToArray(), CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri), log);
                exitCode.Should().Be(0);
                File.Exists(updatedFilesPath).Should().BeTrue();

                // Act
                // Ignore the cursor to process the same catalog entries again.
                args.AddRange(new[] { "--start", "2000-01-01T00:00:00Z" });
                exitCode = await NuGetMirror.Program.MainCore(args.ToArray(), CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri), log);

                // Assert
                exitCode.Should().Be(0);
                File.Exists(updatedFilesPath).Should().BeFalse("the package did not change");
            }
        }

        [Theory]
        [InlineData("v2")]
        [InlineData("v3")]
        public async Task VerifyReplacedPackagesAreUpdatedWhenTheExistingFileWasCreatedAfterTheCommit(string folderFormat)
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var workingDir = new TestFolder())
            {
                var catalogLog = new TestLogger();
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                var nupkgsOutFolder = Path.Combine(workingDir, "nupkgsout");
                var updatedFilesPath = Path.Combine(nupkgsOutFolder, "updatedFiles.txt");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(nupkgsOutFolder);

                TestNupkg.Save(nupkgsFolder, new TestNupkg("a", "1.0.0"));

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var args = new List<string> { "nupkgs", "-o", nupkgsOutFolder, "--folder-format", folderFormat, feedUri.AbsoluteUri, "--delay", "0" };
                var exitCode = await NuGetMirror.Program.MainCore(args.ToArray(), CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri), log);
                exitCode.Should().Be(0);

                // Simulate a copied mirror, the copy is created after the commit and the write time is older than the catalog entry.
                var nupkgPath = Directory.GetFiles(nupkgsOutFolder, "*.nupkg", SearchOption.AllDirectories).Single();
                var commitTime = File.GetLastWriteTimeUtc(nupkgPath);
                File.SetCreationTimeUtc(nupkgPath, commitTime.AddHours(1));
                File.SetLastWriteTimeUtc(nupkgPath, commitTime.AddHours(-1));

                // v3 only, these should be rewritten when the nupkg is replaced.
                var hashPaths = Directory.GetFiles(nupkgsOutFolder, "*.sha512", SearchOption.AllDirectories);
                foreach (var hashPath in hashPaths)
                {
                    File.WriteAllText(hashPath, "stale");
                }

                // Act
                // Ignore the cursor to process the same catalog entries again.
                args.AddRange(new[] { "--start", "2000-01-01T00:00:00Z" });
                exitCode = await NuGetMirror.Program.MainCore(args.ToArray(), CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri), log);

                // Assert
                exitCode.Should().Be(0);
                File.GetLastWriteTimeUtc(nupkgPath).Should().Be(commitTime, "the package was replaced");
                File.Exists(updatedFilesPath).Should().BeTrue("the package was replaced");
                File.ReadAllLines(updatedFilesPath).Select(Path.GetFileName).Should().Equal("a.1.0.0.nupkg");
                hashPaths.Select(File.ReadAllText).Should().NotContain("stale");
            }
        }

        [Fact]
        public async Task GivenLatestOnlyOptionVerifyDownloadsOnlyLatest()
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

                var packageA1 = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA1);
                var packageA2 = new TestNupkg("a", "2.0.0");
                TestNupkg.Save(nupkgsFolder, packageA2);

                var packageB1 = new TestNupkg("b", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageB1);
                var packageB2 = new TestNupkg("b", "2.0.0");
                TestNupkg.Save(nupkgsFolder, packageB2);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, catalogLog);
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                var args = new string[] { "nupkgs", "-o", nupkgsOutFolder, feedUri.AbsoluteUri, "--delay", "0", "--latest-only" };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                var results = LocalFolderUtility.GetPackagesV3(nupkgsOutFolder, catalogLog, TestContext.Current.CancellationToken).ToList();

                results.Select(e => e.Identity).Should().BeEquivalentTo(
                    new[] {
                        new PackageIdentity("a", NuGetVersion.Parse("2.0.0")),
                        new PackageIdentity("b", NuGetVersion.Parse("2.0.0"))
                    });
            }
        }
    }
}
