global using Xunit;
global using Xunit.Abstractions;
using FluentAssertions;
using System.Runtime.CompilerServices;

namespace AsyncQueue.Tests;

public class AsyncQueueTests(ITestOutputHelper output)
{
    private readonly AsyncQueue<int> q = new();

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TestQueue(int count)
    {
        for (int i = 0; i < count; i++)
        {
            await Enqueue(i);
        }

        for (int i = 0; i < count; i++)
        {
            await AssertDequeue(i);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TestAsyncQueue(int count)
    {
        async Task EnqueueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await Enqueue(i);
                await Task.Yield();
            }
        }

        async Task DequeueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await AssertDequeue(i);
            }
        }

        Func<Task> act = async () => await Task.WhenAll([
                EnqueueMany(),
                DequeueMany()
            ]);

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(2));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task TestQueueAfterDequeue(int count)
    {
        async Task DequeueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await AssertDequeue(i);
            }
        }

        async Task EnqueueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await Task.Delay(1);
                await Enqueue(i);
            }
        }

        async Task DoTheThing()
        {
            await Task.WhenAll([
                EnqueueMany(),
                DequeueMany()
            ]);
        }

        Func<Task> act = DoTheThing;

        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task TestCancellation(int count)
    {
        CancellationTokenSource cts = new();
        CancellationToken cancellationToken = cts.Token;

        async Task DequeueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await AssertDequeue(i, cancellationToken);
            }
        }

        async Task EnqueueMany()
        {
            for (int i = 0; i < count; i++)
            {
                await Enqueue(i);
                if (i >= (count / 2))
                {
                    output.WriteLine("Canceled {0}", i);
                    cts.Cancel();
                }
                await Task.Yield();
            }
        }

        async Task DoTheThing()
        {
            await Task.WhenAll([
                DequeueMany(),
                EnqueueMany()
            ]);
        }

        Func<Task> act = DoTheThing;

        await act.Should().ThrowAsync<TaskCanceledException>();
    }

    private async Task Enqueue(int val)
    {
        output.WriteLine("Enqueue {0}", val);
        await q.EnqueueAsync(val);
    }

    private async Task AssertDequeue(int expected, CancellationToken cancellationToken = default)
    {
        output.WriteLine("Dequeue {0}", expected);

        var actual = await q.DequeueAsync(cancellationToken);

        actual.Should().Be(expected);
    }

    [Fact]
    public void CanCreateWithSize()
    {
        var _ = new AsyncQueue<int>(42);
    }

    // TODO what _are_ the semantics of iterating a Queue?
    [Fact]
    public async Task Foo()
    {
        var queue = new AsyncQueue<int>([1,2,3]);

        var stopWhenQueueIsEmpty = new CancellationTokenSource();

        await foreach (var item in queue.WithCancellation(stopWhenQueueIsEmpty.Token))
        {
            output.WriteLine("Got {0}", item);
            if (queue.Count == 0)
            {
                stopWhenQueueIsEmpty.Cancel();
            }
        }

        queue.Should().BeEmpty();
    }
}