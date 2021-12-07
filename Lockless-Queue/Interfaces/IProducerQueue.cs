namespace LocklessQueue
{
    /// <summary>
    /// A common interface that represents the producer side of a concurrent collection.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the collection.</typeparam>
    public interface IProducerQueue<T>
    {
        /// <summary>
        /// Gets a value that indicates whether the <see cref="IProducerQueue{T}"/> can be used by multiple threads.
        /// </summary>
        bool IsMultiProducer { get; }

        /// <summary>
        /// Attempts to add the object at the end of the <see cref="IProducerQueue{T}"/>.
        /// </summary>
        bool TryEnqueue(T value);
    }
}
