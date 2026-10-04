using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace NuGet.CatalogReader.Tests
{
    public class TaskUtilsTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task VerifyRunAsyncThrowsWhenCancelledBeforeStarting(bool useTaskRun)
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var runCount = 0;
                var tasks = Enumerable.Range(0, 10).Select(i => new Func<Task<int>>(() =>
                {
                    Interlocked.Increment(ref runCount);
                    return Task.FromResult(i);
                }));

                // Act
                Func<Task> act = () => TaskUtils.RunAsync(tasks, useTaskRun, maxThreads: 4, token: cts.Token);

                // Assert
                await act.Should().ThrowAsync<OperationCanceledException>();
                runCount.Should().Be(0);
            }
        }

        [Fact]
        public async Task VerifyRunAsyncStopsStartingWorkWhenCancelled()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                var started = new List<int>();
                var tasks = Enumerable.Range(0, 5).Select(i => new Func<Task<int>>(() =>
                {
                    started.Add(i);
                    cts.Cancel();
                    return Task.FromResult(i);
                }));

                // Act
                Func<Task> act = () => TaskUtils.RunAsync(tasks, useTaskRun: false, maxThreads: 1, token: cts.Token);

                // Assert
                await act.Should().ThrowAsync<OperationCanceledException>();
                started.Should().Equal(0);
            }
        }

        [Fact]
        public async Task VerifyRunAsyncReturnsResultsWhenCancelledAfterTheLastItemStarted()
        {
            // Arrange
            using (var cts = new CancellationTokenSource())
            {
                var tasks = Enumerable.Range(0, 3).Select(i => new Func<Task<int>>(() =>
                {
                    if (i == 2)
                    {
                        cts.Cancel();
                    }

                    return Task.FromResult(i);
                }));

                // Act
                var results = await TaskUtils.RunAsync(tasks, useTaskRun: false, maxThreads: 1, token: cts.Token);

                // Assert
                results.Should().Equal(0, 1, 2);
            }
        }
    }
}
