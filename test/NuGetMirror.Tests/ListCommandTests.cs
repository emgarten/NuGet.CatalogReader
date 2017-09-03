using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Sleet;
using Test.Common;
using Xunit;


namespace NuGetMirror.Tests
{
    public class ListCommandTests
    {
        [Fact]
        public async Task GivenACatalogVerifyPackagesShownFromList()
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

                var args = new string[] { "list", feedUri.AbsoluteUri };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                log.GetMessages().Should().Contain("a 1.0.0");
            }
        }

        [Fact]
        public async Task GivenACatalogVerifyPackagesStartDateInFutureDisplaysZeroEntries()
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

                var future = DateTimeOffset.UtcNow.AddMinutes(1);

                var args = new string[] { "list", feedUri.AbsoluteUri, "-s", future.ToString("O") };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                log.GetMessages().Should().NotContain("a 1.0.0");
            }
        }

        [Fact]
        public async Task GivenACatalogVerifyPackagesEndDateInPastDisplaysZeroEntries()
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

                var past = DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(1));

                var args = new string[] { "list", feedUri.AbsoluteUri, "-e", past.ToString("O") };
                var exitCode = await NuGetMirror.Program.MainCore(args, httpSource, log);

                exitCode.Should().Be(0);

                log.GetMessages().Should().NotContain("a 1.0.0");
            }
        }
    }
}
