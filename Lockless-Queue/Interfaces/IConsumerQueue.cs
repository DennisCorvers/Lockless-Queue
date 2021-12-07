using System;
using System.Collections.Generic;

namespace LocklessQueue
{
    /// <summary>
    /// A common interface that represents the consumer side of a concurrent queue.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    public interface IConsumerQueue<T> : IEnumerable<T>
    {
        /// <summary>
        /// Gets a value that indicates whether the <see cref="IConsumerQueue{T}"/> can be used by multiple threads.
        /// </summary>
        bool IsMultiConsumer { get; }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="IConsumerQueue{T}"/> is empty.
        /// </summary>
        bool IsEmpty { get; }

        /// <summary>
        /// Attempts to add the object at the end of the <see cref="IConsumerQueue{T}"/>.
        /// </summary>
        bool TryDequeue(out T value);

        /// <summary>
        /// Attempts to return an object from the beginning of the <see cref="IConsumerQueue{T}"/>
        /// without removing it.
        /// </summary>
        bool TryPeek(out T value);

        /// <summary>
        /// Copies the elements stored in the <see cref="IConsumerQueue{T}"/> to a new array.
        /// </summary>
        T[] ToArray();

        /// <summary>
        /// Copies the <see cref="IConsumerQueue{T}"/> elements to an existing one-dimensional <see
        /// cref="Array">Array</see>, starting at the specified array index.
        /// </summary>
        void CopyTo(T[] array, int index);
    }
}
