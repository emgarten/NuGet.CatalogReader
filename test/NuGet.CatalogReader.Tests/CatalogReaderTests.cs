using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Helpers;
using Sleet;
using Test.Common;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class CatalogReaderTests
    {
        [Fact]
        public async Task VerifyNoEntriesWhenReadingAnEmptyCatalogAsync()
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
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync(TestContext.Current.CancellationToken);
                    var set = await catalogReader.GetPackageSetAsync(TestContext.Current.CancellationToken);

                    // Assert
                    Assert.Empty(entries);
                    Assert.Empty(flatEntries);
                    Assert.Empty(set);
                }
            }
        }

        [Fact]
        public async Task VerifySingleEntriesWhenReadingACatalogAsync()
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

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync(TestContext.Current.CancellationToken);
                    var set = await catalogReader.GetPackageSetAsync(TestContext.Current.CancellationToken);

                    var entry = entries.FirstOrDefault();

                    // Assert
                    Assert.Single(entries);
                    Assert.Single(flatEntries);
                    Assert.Single(set);

                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0", entry.Version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async Task VerifyDownloadNuspec()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    // Act
                    var fileInfo = await entry.DownloadNuspecAsync(
                        downloadFolder,
                        DownloadMode.Force,
                        CancellationToken.None);

                    // Assert
                    var reader = new NuspecReader(fileInfo.FullName);
                    Assert.Equal("a", reader.GetId());
                });
        }

        [Fact]
        public async Task VerifyDownloadModeSkipIfExists_DoesNotExist()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.SkipIfExists,
                        CancellationToken.None);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        Assert.Equal("a", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeSkipIfExists_Exists()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);

                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.SkipIfExists,
                        CancellationToken.None);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        Assert.Equal("different", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeOverwriteIfNewer_ExistingIsOlder()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);
                    File.SetLastWriteTimeUtc(nupkgPath, DateTime.UtcNow.AddDays(-2));

                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.OverwriteIfNewer,
                        CancellationToken.None);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        Assert.Equal("a", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeOverwriteIfNewer_ExistingIsNewer()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);
                    File.SetLastWriteTimeUtc(nupkgPath, DateTime.UtcNow.AddDays(2));

                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.OverwriteIfNewer,
                        CancellationToken.None);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        Assert.Equal("different", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeFailIfExists_DoesNotExist()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.FailIfExists,
                        CancellationToken.None);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        Assert.Equal("a", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeFailIfExists_Exists()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);

                    // Act & Assert
                    await Assert.ThrowsAsync<InvalidOperationException>(() => entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.FailIfExists,
                        CancellationToken.None));
                    using (var reader = new PackageArchiveReader(nupkgPath))
                    {
                        Assert.Equal("different", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadModeForce()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);

                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.Force,
                        CancellationToken.None);

                    // Assert
                    Assert.NotNull(fileInfo);
                    Assert.Equal(nupkgPath, fileInfo.FullName);
                    using (var reader = new PackageArchiveReader(nupkgPath))
                    {
                        Assert.Equal("a", reader.NuspecReader.GetId());
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadNupkgUsesTheCommitTimeStamp()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.FailIfExists,
                        TestContext.Current.CancellationToken);

                    // Assert
                    File.GetLastWriteTimeUtc(fileInfo.FullName).Should().Be(entry.CommitTimeStamp.UtcDateTime);
                });
        }

        [Fact]
        public async Task VerifyDownloadModeOverwriteIfNewer_ExistingHasTheCommitTimeStamp()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nupkgPath = Path.Combine(downloadFolder, $"{entry.FileBaseName}.nupkg");
                    var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                    File.Move(testNupkg.FullName, nupkgPath);
                    File.SetLastWriteTimeUtc(nupkgPath, entry.CommitTimeStamp.UtcDateTime);

                    // Act
                    var fileInfo = await entry.DownloadNupkgAsync(
                        downloadFolder,
                        DownloadMode.OverwriteIfNewer,
                        TestContext.Current.CancellationToken);

                    // Assert
                    using (var reader = new PackageArchiveReader(fileInfo.FullName))
                    {
                        reader.NuspecReader.GetId().Should().Be("different");
                    }
                });
        }

        [Fact]
        public async Task VerifyDownloadNuspecCreatesTheOutputFolder()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var outputFolder = Path.Combine(downloadFolder, "nested", "folder");

                    // Act
                    var fileInfo = await entry.DownloadNuspecAsync(
                        outputFolder,
                        DownloadMode.FailIfExists,
                        TestContext.Current.CancellationToken);

                    // Assert
                    fileInfo.FullName.Should().Be(Path.Combine(outputFolder, "a.1.0.0.nuspec"));
                    new NuspecReader(fileInfo.FullName).GetId().Should().Be("a");
                    File.GetLastWriteTimeUtc(fileInfo.FullName).Should().Be(entry.CommitTimeStamp.UtcDateTime);
                });
        }

        [Theory]
        [InlineData(DownloadMode.Force, 0, "a")]
        [InlineData(DownloadMode.SkipIfExists, -48, "different")]
        [InlineData(DownloadMode.OverwriteIfNewer, -48, "a")]
        [InlineData(DownloadMode.OverwriteIfNewer, 0, "different")]
        public async Task VerifyDownloadNuspecModesWhenTheFileExists(DownloadMode mode, int existingFileOffsetHours, string expectedId)
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nuspecPath = Path.Combine(downloadFolder, "a.1.0.0.nuspec");
                    WriteNuspec(nuspecPath, "different");
                    File.SetLastWriteTimeUtc(nuspecPath, entry.CommitTimeStamp.UtcDateTime.AddHours(existingFileOffsetHours));

                    // Act
                    var fileInfo = await entry.DownloadNuspecAsync(
                        downloadFolder,
                        mode,
                        TestContext.Current.CancellationToken);

                    // Assert
                    fileInfo.FullName.Should().Be(nuspecPath);
                    new NuspecReader(nuspecPath).GetId().Should().Be(expectedId);
                });
        }

        [Fact]
        public async Task VerifyDownloadNuspecFailIfExists_Exists()
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nuspecPath = Path.Combine(downloadFolder, "a.1.0.0.nuspec");
                    WriteNuspec(nuspecPath, "different");

                    // Act
                    Func<Task> act = () => entry.DownloadNuspecAsync(
                        downloadFolder,
                        DownloadMode.FailIfExists,
                        TestContext.Current.CancellationToken);

                    // Assert
                    await act.Should().ThrowAsync<InvalidOperationException>();
                    new NuspecReader(nuspecPath).GetId().Should().Be("different");
                });
        }

        [Theory]
        [InlineData("not xml")]
        [InlineData("<html />")]
        [InlineData("<package><metadata><id>a</id></metadata></package>")]
        [InlineData("<package><metadata><id></id><version>1.0.0</version></metadata></package>")]
        public async Task VerifyDownloadNuspecReplacesAnInvalidFile(string content)
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var nuspecPath = Path.Combine(downloadFolder, "a.1.0.0.nuspec");
                    File.WriteAllText(nuspecPath, content);

                    // Act
                    await entry.DownloadNuspecAsync(
                        downloadFolder,
                        DownloadMode.FailIfExists,
                        TestContext.Current.CancellationToken);

                    // Assert
                    new NuspecReader(nuspecPath).GetId().Should().Be("a");
                });
        }

        [Theory]
        [InlineData(".nupkg", false)]
        [InlineData(".nupkg", true)]
        [InlineData(".nuspec", false)]
        [InlineData(".nuspec", true)]
        public async Task VerifyDownloadReturnsTheCurrentFileState(string extension, bool fileExists)
        {
            // Arrange
            await VerifyDownloadMode(
                async (downloadFolder, entry) =>
                {
                    var path = Path.Combine(downloadFolder, "a.1.0.0" + extension);

                    if (fileExists)
                    {
                        if (extension == ".nupkg")
                        {
                            var testNupkg = TestNupkg.Create("different", "1.0.0").Save(downloadFolder);
                            File.Move(testNupkg.FullName, path);
                        }
                        else
                        {
                            WriteNuspec(path, "different");
                        }

                        File.SetLastWriteTimeUtc(path, entry.CommitTimeStamp.UtcDateTime.AddHours(-48));
                    }

                    // Act
                    var fileInfo = extension == ".nupkg"
                        ? await entry.DownloadNupkgAsync(downloadFolder, DownloadMode.Force, TestContext.Current.CancellationToken)
                        : await entry.DownloadNuspecAsync(downloadFolder, DownloadMode.Force, TestContext.Current.CancellationToken);

                    // Assert
                    fileInfo.FullName.Should().Be(path);
                    fileInfo.Exists.Should().BeTrue();
                    fileInfo.Length.Should().Be(new FileInfo(path).Length);
                    fileInfo.LastWriteTimeUtc.Should().Be(entry.CommitTimeStamp.UtcDateTime);
                });
        }

        [Fact]
        public async Task VerifyGetEntriesAsyncThrowsWhenCancelled()
        {
            // Arrange
            using (var cache = new LocalCache())
            using (var cacheContext = new SourceCacheContext())
            using (var workingDir = new TestFolder())
            using (var cts = new CancellationTokenSource())
            {
                var log = new TestLogger();
                var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
                var feedFolder = Path.Combine(workingDir, "feed");
                var nupkgsFolder = Path.Combine(workingDir, "nupkgs");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);

                TestNupkg.Save(nupkgsFolder, new TestNupkg("a", "1.0.0"));

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var pages = await catalogReader.GetPageEntriesAsync(TestContext.Current.CancellationToken);
                    pages.Should().NotBeEmpty();
                    cts.Cancel();

                    // Act
                    Func<Task> act = () => catalogReader.GetEntriesAsync(pages, cts.Token);

                    // Assert
                    await act.Should().ThrowAsync<OperationCanceledException>();
                }
            }
        }

        private static void WriteNuspec(string path, string id)
        {
            File.WriteAllText(path, $"<?xml version=\"1.0\" encoding=\"utf-8\"?><package><metadata><id>{id}</id><version>1.0.0</version></metadata></package>");
        }

        private static async Task VerifyDownloadMode(
            Func<string, CatalogEntry, Task> actAndAssertAsync)
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
                var downloadFolder = Path.Combine(workingDir, "download");
                Directory.CreateDirectory(feedFolder);
                Directory.CreateDirectory(nupkgsFolder);
                Directory.CreateDirectory(downloadFolder);

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync();
                    var entry = entries.FirstOrDefault();
                    await actAndAssertAsync(downloadFolder, entry);
                }
            }
        }

        [Fact]
        public async Task VerifyEditsAreIgnoredInFlattenedViewAsync()
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

                var packageA = new TestNupkg("a", "1.0.0");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                // 2nd push
                await CatalogReaderTestHelpers.PushPackagesAsync(workingDir, nupkgsFolder, baseUri, log);

                // 3rd push
                await CatalogReaderTestHelpers.PushPackagesAsync(workingDir, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var flatEntries = await catalogReader.GetFlattenedEntriesAsync(TestContext.Current.CancellationToken);
                    var set = await catalogReader.GetPackageSetAsync(TestContext.Current.CancellationToken);

                    var entry = entries.FirstOrDefault();

                    // Assert
                    // 3 adds, 2 removes
                    Assert.Equal(5, entries.Count);
                    Assert.Single(flatEntries);
                    Assert.Single(set);

                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0", entry.Version.ToNormalizedString());
                }
            }
        }

        [Fact]
        public async Task VerifyCatalogEntryPropertiesAsync()
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

                var packageA = new TestNupkg("a", "1.0.0.1-RC.1.2.b0.1+meta.blah.1");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var entry = entries.FirstOrDefault();

                    // Assert
                    Assert.Equal("a", entry.Id);
                    Assert.Equal("1.0.0.1-RC.1.2.b0.1", entry.Version.ToNormalizedString());
                    Assert.NotEmpty(entry.CommitId);
                    Assert.True(DateTimeOffset.MinValue < entry.CommitTimeStamp);
                    Assert.Equal("a.1.0.0.1-rc.1.2.b0.1", entry.FileBaseName);
                    Assert.True(entry.IsAddOrUpdate);
                    Assert.False(entry.IsDelete);
                    Assert.True(await entry.IsListedAsync(TestContext.Current.CancellationToken));
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/1.0.0.1-rc.1.2.b0.1/a.1.0.0.1-rc.1.2.b0.1.nupkg", entry.NupkgUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/1.0.0.1-rc.1.2.b0.1/a.nuspec", entry.NuspecUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/flatcontainer/a/index.json", entry.PackageBaseAddressIndexUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/registration/a/1.0.0.1-rc.1.2.b0.1.json", entry.PackageRegistrationUri.AbsoluteUri);
                    Assert.Equal("https://localhost:8080/testFeed/registration/a/index.json", entry.RegistrationIndexUri.AbsoluteUri);
                    Assert.Equal("nuget:PackageDetails", string.Join("|", entry.Types));
                    Assert.StartsWith("https://localhost:8080/testFeed/catalog/data/", entry.Uri.AbsoluteUri);
                }
            }
        }

        [Fact]
        public async Task GetCatalogEntryVerifyUrlsCanBeOpenedAsJsonAsync()
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

                var packageA = new TestNupkg("a", "1.0.0.1-RC.1.2.b0.1+meta.blah.1");
                TestNupkg.Save(nupkgsFolder, packageA);

                // Create and push
                await CatalogReaderTestHelpers.CreateCatalogAsync(workingDir, feedFolder, nupkgsFolder, baseUri, log);

                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                // Act
                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var entries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var entry = entries.FirstOrDefault();

                    // Assert
                    (await entry.GetNupkgAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetNupkgAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetNuspecAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetPackageBaseAddressIndexUriAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetPackageDetailsAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetPackageRegistrationUriAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                    (await entry.GetRegistrationIndexUriAsync(TestContext.Current.CancellationToken)).Should().NotBeNull();
                }
            }
        }

        [Fact]
        public async Task VerifyStartTimeIsExclusiveAndEndTimeIsInclusive()
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

                const int packageCount = 10;
                await CatalogReaderTestHelpers.CreateCatalogAsync(
                    workingDir,
                    feedFolder,
                    nupkgsFolder,
                    baseUri,
                    catalogPageSize: 2,
                    log: log);
                
                foreach (var i in Enumerable.Range(0, packageCount))
                {
                    var nupkgFolder = Path.Combine(nupkgsFolder, i.ToString());
                    TestNupkg.Save(nupkgFolder, new TestNupkg($"Package{i}", "1.0.0"));
                    await CatalogReaderTestHelpers.PushPackagesAsync(workingDir, nupkgFolder, baseUri, log);
                }
                
                var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");
                var httpSource = CatalogReaderTestHelpers.GetHttpSource(cache, feedFolder, baseUri);

                using (var catalogReader = new CatalogReader(feedUri, httpSource, cacheContext, TimeSpan.FromMinutes(1), log))
                {
                    var allEntries = await catalogReader.GetEntriesAsync(TestContext.Current.CancellationToken);
                    var timestamps = allEntries
                        .OrderBy(x => x.CommitTimeStamp)
                        .Select(x => x.CommitTimeStamp)
                        .ToList();

                    var start = timestamps[2];
                    var end = timestamps[packageCount - 3];

                    // Act
                    var entries = await catalogReader.GetEntriesAsync(start, end, CancellationToken.None);

                    // Assert
                    packageCount.Should().Be(timestamps.Distinct().Count());
                    timestamps.Skip(3).Take(5).Should().BeEquivalentTo(entries.Select(x => x.CommitTimeStamp));
                }
            }
        }

        [Fact]
        public void DisposeDoesNotThrowWhenNothingHasBeenDone()
        {
            // Arrange
            var baseUri = Sleet.UriUtility.CreateUri("https://localhost:8080/testFeed/");
            var feedUri = Sleet.UriUtility.CreateUri(baseUri.AbsoluteUri + "index.json");

            using (var catalogReader = new CatalogReader(feedUri))
            {
                // Act & Assert
                catalogReader.Dispose();

                // If this does not throw, we're good!
            }
        }
    }
}
