namespace LocklessQueue
{
    /// <summary>
    /// A common interface used for concurrent queues.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    public interface IProducerConsumerQueue<T> : IProducerQueue<T>, IConsumerQueue<T>
    {
    }
}
