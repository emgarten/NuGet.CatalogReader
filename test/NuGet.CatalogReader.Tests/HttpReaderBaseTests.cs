using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class HttpReaderBaseTests
    {
        private static readonly Uri IndexUri = new Uri("https://localhost:8080/testFeed/index.json");
        private static readonly Uri PackageIndexUri = new Uri("https://localhost:8080/testFeed/flatcontainer/a/index.json");

        [Theory]
        [InlineData(60, false, 1)]
        [InlineData(0, false, 2)]
        [InlineData(60, true, 2)]
        public async Task VerifyCacheTimeoutControlsResponseCaching(int cacheTimeoutMinutes, bool noCache, int expectedRequests)
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext() { NoCache = noCache })
            using (var workingDir = new TestFolder())
            using (var clientHandler = new HttpClientHandler())
            {
                var handler = CreateHandler();
                var httpSource = CreateHttpSource(clientHandler, handler, Path.Combine(workingDir, "cache"));

                using (var feedReader = new FeedReader(IndexUri, httpSource, cacheContext, TimeSpan.FromMinutes(cacheTimeoutMinutes), new TestLogger()))
                {
                    // Act
                    await feedReader.GetPackagesById("a", TestContext.Current.CancellationToken);
                    var packages = await feedReader.GetPackagesById("a", TestContext.Current.CancellationToken);

                    // Assert
                    packages.Select(e => e.Version.ToNormalizedString()).Should().Equal("1.0.0");
                    handler.GetRequestCount(PackageIndexUri).Should().Be(expectedRequests);
                    cacheContext.MaxAge.Should().BeNull("the caller's cache context should not be modified");
                }
            }
        }

        [Fact]
        public async Task VerifyClearCacheRemovesCachedResponses()
        {
            // Arrange
            using (var cacheContext = new SourceCacheContext())
            using (var clientHandler = new HttpClientHandler())
            {
                var handler = CreateHandler();
                var httpSource = CreateHttpSource(clientHandler, handler, cacheContext.GeneratedTempFolder);

                using (var feedReader = new FeedReader(IndexUri, httpSource, cacheContext, TimeSpan.FromHours(1), new TestLogger()))
                {
                    await feedReader.GetPackagesById("a", TestContext.Current.CancellationToken);
                    Directory.GetFiles(feedReader.HttpCacheFolder, "*", SearchOption.AllDirectories).Should().NotBeEmpty();

                    // Act
                    feedReader.ClearCache();

                    // Assert
                    Directory.GetFiles(feedReader.HttpCacheFolder, "*", SearchOption.AllDirectories).Should().BeEmpty();
                    await feedReader.GetPackagesById("a", TestContext.Current.CancellationToken);
                    handler.GetRequestCount(PackageIndexUri).Should().Be(2);
                }
            }
        }

        [Fact]
        public async Task VerifyCreatedHttpSourceCachesResponsesInHttpCacheFolder()
        {
            // Arrange
            using (var feedReader = new FeedReader(IndexUri, TimeSpan.FromHours(1)))
            {
                // Act
                var httpSource = await feedReader.GetHttpSourceAsync();

                // Assert
                httpSource.HttpCacheDirectory.Should().Be(feedReader.HttpCacheFolder);
            }
        }

        private static FakeHttpMessageHandler CreateHandler()
        {
            var responses = new Dictionary<Uri, string>()
            {
                {
                    IndexUri,
                    "{\"version\": \"3.0.0\", \"resources\": [{\"@id\": \"https://localhost:8080/testFeed/flatcontainer/\", \"@type\": \"PackageBaseAddress/3.0.0\"}]}"
                },
                {
                    PackageIndexUri,
                    "{\"versions\": [\"1.0.0\"]}"
                },
            };

            return new FakeHttpMessageHandler(responses);
        }

        private static HttpSource CreateHttpSource(HttpClientHandler clientHandler, HttpMessageHandler handler, string cacheDirectory)
        {
            return new HttpSource(
                new PackageSource(IndexUri.AbsoluteUri),
                () => Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(clientHandler, handler)),
                NullThrottle.Instance)
            {
                HttpCacheDirectory = cacheDirectory,
            };
        }
    }
}
