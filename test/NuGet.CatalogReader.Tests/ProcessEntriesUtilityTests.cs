using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class ProcessEntriesUtilityTests
    {
        [Fact]
        public async Task VerifyRunAsyncThrowsWhenCancelledBeforeStarting()
        {
            // Arrange
            var entries = CreateEntries(5);
            var applied = 0;

            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act
                Func<Task> act = () => ProcessEntriesUtility.RunAsync(
                    e =>
                    {
                        Interlocked.Increment(ref applied);
                        return Task.FromResult(e.Id);
                    },
                    maxThreads: 2,
                    entries: entries,
                    token: cts.Token);

                // Assert
                await act.Should().ThrowAsync<OperationCanceledException>();
                applied.Should().Be(0);
            }
        }

        [Fact]
        public async Task VerifyRunAsyncStopsStartingEntriesWhenCancelled()
        {
            // Arrange
            var entries = CreateEntries(5);
            var applied = 0;

            using (var cts = new CancellationTokenSource())
            {
                // Act
                Func<Task> act = () => ProcessEntriesUtility.RunAsync(
                    e =>
                    {
                        Interlocked.Increment(ref applied);
                        cts.Cancel();
                        return Task.FromResult(e.Id);
                    },
                    maxThreads: 1,
                    entries: entries,
                    token: cts.Token);

                // Assert
                await act.Should().ThrowAsync<OperationCanceledException>();
                applied.Should().Be(1);
            }
        }

        private static CatalogEntry[] CreateEntries(int count)
        {
            var commitTime = DateTimeOffset.UtcNow;

            return Enumerable.Range(0, count)
                .Select(i => TestEntryFactory.CreateCatalogEntry($"package{i}", "1.0.0", commitTime.AddMinutes(i)))
                .ToArray();
        }
    }
}
