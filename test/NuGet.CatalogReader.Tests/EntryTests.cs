using System;
using System.Threading.Tasks;
using AwesomeAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class EntryTests
    {
        [Theory]
        [InlineData("{}", true)]
        [InlineData("{\"listed\": null}", true)]
        [InlineData("{\"listed\": true}", true)]
        [InlineData("{\"listed\": false}", false)]
        public async Task VerifyIsListedAsyncReadsTheRegistrationLeaf(string registrationJson, bool expected)
        {
            // Arrange
            Uri requestedUri = null;
            var entry = TestEntryFactory.CreatePackageEntry(
                "a",
                "1.0.0",
                getJson: (uri, token) =>
                {
                    requestedUri = uri;
                    return Task.FromResult(JObject.Parse(registrationJson));
                });

            // Act
            var listed = await entry.IsListedAsync(TestContext.Current.CancellationToken);

            // Assert
            listed.Should().Be(expected);
            requestedUri.Should().Be(entry.PackageRegistrationUri);
        }

        [Fact]
        public void VerifyPackageEntryComparisonsWithNull()
        {
            // Arrange
            var entry = TestEntryFactory.CreatePackageEntry("a", "1.0.0");
            PackageEntry nullEntry = null;

            // Act & Assert
            entry.CompareTo(nullEntry).Should().BePositive();
            (entry < nullEntry).Should().BeFalse();
            (entry <= nullEntry).Should().BeFalse();
            (entry > nullEntry).Should().BeTrue();
            (entry >= nullEntry).Should().BeTrue();
            (nullEntry < entry).Should().BeTrue();
            (nullEntry <= entry).Should().BeTrue();
            (nullEntry > entry).Should().BeFalse();
            (nullEntry >= entry).Should().BeFalse();
        }

        [Fact]
        public void VerifyCatalogEntryComparisonsWithNull()
        {
            // Arrange
            var entry = TestEntryFactory.CreateCatalogEntry("a", "1.0.0", DateTimeOffset.UtcNow);
            CatalogEntry nullEntry = null;

            // Act & Assert
            entry.CompareTo(nullEntry).Should().BePositive();
            (entry < nullEntry).Should().BeFalse();
            (entry <= nullEntry).Should().BeFalse();
            (entry > nullEntry).Should().BeTrue();
            (entry >= nullEntry).Should().BeTrue();
            (nullEntry < entry).Should().BeTrue();
            (nullEntry <= entry).Should().BeTrue();
            (nullEntry > entry).Should().BeFalse();
            (nullEntry >= entry).Should().BeFalse();
        }

        [Fact]
        public void VerifyCatalogPageEntryComparisonsWithNull()
        {
            // Arrange
            var entry = TestEntryFactory.CreateCatalogPageEntry(0, DateTimeOffset.UtcNow);
            CatalogPageEntry nullEntry = null;

            // Act & Assert
            entry.CompareTo(nullEntry).Should().BePositive();
            (entry < nullEntry).Should().BeFalse();
            (entry <= nullEntry).Should().BeFalse();
            (entry > nullEntry).Should().BeTrue();
            (entry >= nullEntry).Should().BeTrue();
            (nullEntry < entry).Should().BeTrue();
            (nullEntry <= entry).Should().BeTrue();
            (nullEntry > entry).Should().BeFalse();
            (nullEntry >= entry).Should().BeFalse();
        }

        [Fact]
        public void VerifyComparisonsWithBothOperandsNull()
        {
            // Arrange
            PackageEntry leftEntry = null;
            PackageEntry rightEntry = null;
            CatalogPageEntry leftPage = null;
            CatalogPageEntry rightPage = null;

            // Act & Assert
            (leftEntry < rightEntry).Should().BeFalse();
            (leftEntry <= rightEntry).Should().BeTrue();
            (leftEntry > rightEntry).Should().BeFalse();
            (leftEntry >= rightEntry).Should().BeTrue();
            (leftPage < rightPage).Should().BeFalse();
            (leftPage <= rightPage).Should().BeTrue();
            (leftPage > rightPage).Should().BeFalse();
            (leftPage >= rightPage).Should().BeTrue();
        }
    }
}
