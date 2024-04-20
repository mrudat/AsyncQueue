namespace AsyncQueue;

public class AsyncQueue<T>
{
    private readonly Queue<T> itemQueue = new();
    private readonly Queue<TaskCompletionSource<T>> waiterQueue = new();

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

    public async Task<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        if (itemQueue.TryDequeue(out var item))
            return item;

        var taskCompletionSource = new TaskCompletionSource<T>();

        waiterQueue.Enqueue(taskCompletionSource);

        item = await taskCompletionSource.Task.WaitAsync(cancellationToken);

        await Task.Yield();

        return item;
    }
}
