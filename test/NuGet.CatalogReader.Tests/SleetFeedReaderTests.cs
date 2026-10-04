using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Sleet;
using Test.Common;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class SleetFeedReaderTests
    {
        [Fact]
        public async Task VerifyGetPackagesAsyncReturnsAllPackages()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                TestNupkg.Save(nupkgsFolder, new TestNupkg("a", "1.0.0"));
                TestNupkg.Save(nupkgsFolder, new TestNupkg("a", "2.0.0-beta"));
                TestNupkg.Save(nupkgsFolder, new TestNupkg("b", "1.0.0"));

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var feedReader = new SleetFeedReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var packages = await feedReader.GetPackagesAsync(TestContext.Current.CancellationToken);

                    // Assert
                    packages.Select(e => $"{e.Id} {e.Version.ToNormalizedString()}")
                        .Should().BeEquivalentTo("a 1.0.0", "a 2.0.0-beta", "b 1.0.0");

                    var package = packages.Single(e => e.Id == "b");
                    package.NupkgUri.AbsoluteUri.Should().Be("https://localhost:8080/testFeed/flatcontainer/b/1.0.0/b.1.0.0.nupkg");

                    using (var stream = await package.GetNupkgAsync(TestContext.Current.CancellationToken))
                    {
                        stream.Should().NotBeNull();
                    }
                }
            }
        }

        [Fact]
        public async Task VerifyGetPackagesAsyncReturnsNoPackagesForAnEmptyFeed()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var feedReader = new SleetFeedReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var packages = await feedReader.GetPackagesAsync(TestContext.Current.CancellationToken);

                    // Assert
                    packages.Should().BeEmpty();
                }
            }
        }
    }
}
