using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class NuGetExtensionsTests
    {
        [Fact]
        public async Task VerifyCachedResponsesAreKeyedByTheFullUri()
        {
            // Arrange
            var uris = new[]
            {
                new Uri("https://localhost:8080/testFeed/x_y/index.json"),
                new Uri("https://localhost:8080/testFeed/x/y/index.json"),
                new Uri("https://localhost:8081/testFeed/x/y/index.json"),
                new Uri("https://localhost:8080/testFeed/x/y/index.json?page=2"),
                new Uri($"https://localhost:8080/testFeed/{new string('a', 300)}/index.json"),
            };

            var handler = new FakeHttpMessageHandler(uris.ToDictionary(uri => uri, uri => new JObject() { ["uri"] = uri.AbsoluteUri }.ToString()));

            using (var cacheContext = new SourceCacheContext() { MaxAge = DateTimeOffset.UtcNow.AddHours(-1) })
            using (var workingDir = new TestFolder())
            using (var clientHandler = new HttpClientHandler())
            {
                var log = new TestLogger();
                var httpCacheContext = HttpSourceCacheContext.Create(cacheContext, isFirstAttempt: true);
                var httpSource = new HttpSource(
                    new PackageSource("https://localhost:8080/testFeed/index.json"),
                    () => Task.FromResult<HttpHandlerResource>(new HttpHandlerResourceV3(clientHandler, handler)),
                    NullThrottle.Instance)
                {
                    HttpCacheDirectory = Path.Combine(workingDir, "cache"),
                };

                foreach (var uri in uris)
                {
                    await httpSource.GetJObjectAsync(uri, httpCacheContext, log, TestContext.Current.CancellationToken);
                }

                foreach (var uri in uris)
                {
                    // Act
                    var json = await httpSource.GetJObjectAsync(uri, httpCacheContext, log, TestContext.Current.CancellationToken);

                    // Assert
                    json["uri"]!.ToString().Should().Be(uri.AbsoluteUri);
                    handler.GetRequestCount(uri).Should().Be(1, "the second read should come from the cache");
                }
            }
        }
    }
}
