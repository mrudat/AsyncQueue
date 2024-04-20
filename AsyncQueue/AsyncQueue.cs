using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace AsyncQueue;

/// <inheritdoc cref="System.Collections.Generic.Queue{T}"/>
public class AsyncQueue<T> : IAsyncEnumerable<T>, IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>, ICollection
{
    private readonly Queue<T> itemQueue;
    private readonly Queue<TaskCompletionSource<T>> waiterQueue;

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Queue()"/>
    public AsyncQueue()
    {
        itemQueue = new();
        waiterQueue = new();
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Queue(IEnumerable{T})"/>
    public AsyncQueue(IEnumerable<T> collection)
    {
        itemQueue = new(collection);
        waiterQueue = new(itemQueue.Count);
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Queue(int)"/>
    public AsyncQueue(int capacity)
    {
        itemQueue = new(capacity);
        waiterQueue = new(capacity);
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Count"/>
    public int Count => itemQueue.Count - waiterQueue.Count;

    public bool IsSynchronized => ((ICollection)itemQueue).IsSynchronized;

    public object SyncRoot => ((ICollection)itemQueue).SyncRoot;

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Clear"/>
    public void Clear() => itemQueue.Clear();

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Contains(T)"/>
    public bool Contains(T item) => itemQueue.Contains(item);

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.CopyTo(T[], int)"/>
    public void CopyTo(T[] array, int arrayIndex) => itemQueue.CopyTo(array, arrayIndex);

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Dequeue"/>
    public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        if (itemQueue.TryDequeue(out var item))
            return item;

        var taskCompletionSource = new TaskCompletionSource<T>();

        waiterQueue.Enqueue(taskCompletionSource);

        item = await taskCompletionSource.Task.WaitAsync(cancellationToken);

        // so SetResult will return.
        await Task.Yield();

        return item;
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Enqueue(T)"/>
    public async Task EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        if (waiterQueue.TryDequeue(out var taskCompletionSource))
        {
            cancellationToken.ThrowIfCancellationRequested();
            taskCompletionSource.SetResult(item);
            await Task.Yield();
        }
        else
            itemQueue.Enqueue(item);
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.EnsureCapacity(int)"/>
    public int EnsureCapacity(int capacity)
    {
        waiterQueue.EnsureCapacity(capacity);
        return itemQueue.EnsureCapacity(capacity);
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.Peek"/>
    public T Peek() => itemQueue.Peek();

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.ToArray"/>
#pragma warning disable IDE0305 // Simplify collection initialization
    public T[] ToArray() => itemQueue.ToArray();
#pragma warning restore IDE0305 // Simplify collection initialization

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.TrimExcess"/>
    public void TrimExcess()
    {
        itemQueue.TrimExcess();
        waiterQueue.TrimExcess();
    }

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.TryDequeue(out T)"/>
    public bool TryDequeue([MaybeNullWhen(false)] out T result) => itemQueue.TryDequeue(out result);

    /// <inheritdoc cref="System.Collections.Generic.Queue{T}.TryPeek(out T)"/>
    public bool TryPeek([MaybeNullWhen(false)] out T result) => itemQueue.TryPeek(out result);

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            yield return await DequeueAsync(cancellationToken);
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)itemQueue).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)itemQueue).GetEnumerator();

    public void CopyTo(Array array, int index) => ((ICollection)itemQueue).CopyTo(array, index);
}
