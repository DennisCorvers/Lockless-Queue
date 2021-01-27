using LocklessQueue.Debug;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LocklessQueues
{
    [DebuggerDisplay("Count = {Count}")]
    [DebuggerTypeProxy(typeof(IProducerConsumerCollectionDebugView<>))]
    public class SPSCQueue<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        private T[] m_items;
        private HeadAndTail m_headAndTail;

        /// <summary>
        /// Gets the capacity of this <see cref="SPSCQueue{T}"/>.
        /// </summary>
        public int Capacity
        {
            get => m_items.Length - 1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SPSCQueue{T}"/> class.
        /// </summary>
        /// <param name="capacity">The fixed-capacity of this <see cref="SPSCQueue{T}"/></param>
        public SPSCQueue(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");

            // Reserve one empty slot
            capacity++;
            m_items = new T[capacity];
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SPSCQueue{T}"/> class that contains elements copied
        /// from the specified collection.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new <see cref="SPSCQueue{T}"/>.
        /// </param>
        public SPSCQueue(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            // Reserve one empty slot
            var capacity = collection.Count + 1;

            m_items = new T[capacity];
            collection.CopyTo(m_items, 0);
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="SPSCQueue{T}"/> is empty.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                var nextHead = Volatile.Read(ref m_headAndTail.Head) + 1;

                return Volatile.Read(ref m_headAndTail.Tail) < nextHead;
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="SPSCQueue{T}"/>.
        /// </summary>
        public int Count
        {
            get
            {
                var head = Volatile.Read(ref m_headAndTail.Head);
                var tail = Volatile.Read(ref m_headAndTail.Tail);

                var dif = tail - head;
                if (dif < 0)
                    dif += m_items.Length;

                return dif;
            }
        }

        bool ICollection.IsSynchronized
        {
            get { return false; }
        }
        object ICollection.SyncRoot
        {
            get { throw new NotSupportedException("The SyncRoot property may not be used for the synchronization of concurrent collections."); }
        }

        bool IProducerConsumerCollection<T>.TryAdd(T item)
        {
            return TryEnqueue(item);
        }

        bool IProducerConsumerCollection<T>.TryTake(out T item)
        {
            return TryDequeue(out item);
        }

        void ICollection.CopyTo(Array array, int index)
        {
            GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="SPSCQueue{T}"/>.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Returns an enumerator that iterates through a collection.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Tries to enqueue an element in the queue.
        /// Returns false if the queue is full.
        /// </summary>
        public bool TryEnqueue(T item)
        {
            var tail = Volatile.Read(ref m_headAndTail.Tail);
            var nextTail = GetNext(tail, m_items.Length);

            // Full Queue
            if (nextTail == Volatile.Read(ref m_headAndTail.Head))
                return false;

            m_items[tail] = item;

            Volatile.Write(ref m_headAndTail.Tail, nextTail);
            return true;
        }

        /// <summary>
        /// Tries to dequeue an item from the queue.
        /// Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            var head = Volatile.Read(ref m_headAndTail.Head);

            // Queue empty
            if (Volatile.Read(ref m_headAndTail.Tail) == head)
            {
                item = default;
                return false;
            }

            item = m_items[head];
            var nextHead = GetNext(head, m_items.Length);
            Volatile.Write(ref m_headAndTail.Head, nextHead);

            return true;
        }

        /// <summary>
        /// Tries to peek an item in the queue.
        /// Returns false if the queue if empty.
        /// </summary>
        public bool TryPeek(out T item)
        {
            var head = Volatile.Read(ref m_headAndTail.Head);

            // Queue empty
            if (Volatile.Read(ref m_headAndTail.Tail) == head)
            {
                item = default;
                return false;
            }

            item = m_items[head];

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int GetNext(int value, int length)
        {
            value++;

            if (value == length)
                value = 0;
            return value;
        }

        /// <summary>
        /// Copies the elements stored in the <see cref="SPSCQueue{T}"/> to a new array.
        /// </summary>
        public T[] ToArray()
        {
            var head = Volatile.Read(ref m_headAndTail.Head);
            var tail = Volatile.Read(ref m_headAndTail.Tail);

            var count = tail - head;
            if (count < 0)
                count += m_items.Length;

            if (count <= 0)
                return Array.Empty<T>();

            var arr = new T[count];

            int numToCopy = count;
            int bufferLength = m_items.Length;
            int ihead = head;

            int firstPart = Math.Min(bufferLength - ihead, numToCopy);

            Array.Copy(m_items, ihead, arr, 0, firstPart);
            numToCopy -= firstPart;

            if (numToCopy > 0)
                Array.Copy(m_items, 0, arr, 0 + bufferLength - ihead, numToCopy);

            return arr;
        }

        /// <summary>
        /// Copies the <see cref="SPSCQueue{T}"/> elements to an existing one-dimensional <see cref="Array">Array</see>, starting at the specified array index.
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="Array">Array</see> that is the destination of the elements copied from the
        /// <see cref="SPSCQueue{T}"/>. The <see cref="Array">Array</see> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(Array));

            if ((uint)index > array.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range. Must be non - negative and less than the size of the collection.");

            var head = Volatile.Read(ref m_headAndTail.Head);
            var tail = Volatile.Read(ref m_headAndTail.Tail);

            var count = tail - head;
            if (count < 0)
                count += m_items.Length;

            if (index > array.Length + Count)
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.Check array index and length.");

            if (count <= 0)
                return;

            int numToCopy = count;
            int bufferLength = m_items.Length;
            int ihead = head;

            int firstPart = Math.Min(bufferLength - ihead, numToCopy);

            Array.Copy(m_items, ihead, array, index, firstPart);
            numToCopy -= firstPart;

            if (numToCopy > 0)
                Array.Copy(m_items, 0, array, index + bufferLength - ihead, numToCopy);

            return;
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            // Enumerates over the provided SPSCRingBuffer. Enumeration counts as a READ/Consume operation.
            // The amount of items enumerated can vary depending on if the TAIL moves during enumeration.
            // The HEAD is frozen in place when the enumerator is created. This means that the maximum 
            // amount of items read is always the capacity of the queue and no more.
#pragma warning disable IDE0032
            readonly SPSCQueue<T> m_queue;
            readonly int m_headStart;
            readonly int m_capacity;
            int m_index;
            T m_current;
#pragma warning restore IDE0032

            internal Enumerator(SPSCQueue<T> queue)
            {
                m_queue = queue;
                m_index = -1;
                m_current = default;
                m_capacity = queue.m_items.Length;
                m_headStart = Volatile.Read(ref queue.m_headAndTail.Head);
            }

            public void Dispose()
            {
                m_index = -2;
                m_current = default;
            }

            public bool MoveNext()
            {
                if (m_index == -2)
                    return false;

                var head = Volatile.Read(ref m_queue.m_headAndTail.Head);
                if (m_headStart != head)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                var headIndex = head + ++m_index;

                if (headIndex >= m_capacity)
                {
                    // Wrap around if needed
                    headIndex -= m_capacity;
                }

                // Queue empty
                if (Volatile.Read(ref m_queue.m_headAndTail.Tail) == headIndex)
                {
                    m_current = default;
                    return false;
                }

                m_current = m_queue.m_items[headIndex];

                return true;
            }

            public void Reset()
            {
                m_index = -1;
                m_current = default;
            }

            public T Current => m_current;

            object IEnumerator.Current => Current;
        }
    }
}
