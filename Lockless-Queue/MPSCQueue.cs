using LocklessQueues.QDebug;
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
    public class MPSCQueue<T> : IProducerConsumerCollection<T>, IReadOnlyCollection<T>
    {
        private QueueSlot<T>[] m_items;
        private HeadAndTail m_headAndTail;
        private readonly int m_mask;

        /// <summary>
        /// Gets the capacity of this <see cref="MPSCQueue{T}"/>.
        /// </summary>
        public int Capacity
        {
            get => m_items.Length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MPSCQueue{T}"/> class. Capacity will be set to a power of 2.
        /// </summary>
        /// <param name="capacity">The fixed-capacity of this <see cref="MPSCQueue{T}"/></param>
        public MPSCQueue(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");

            capacity = RoundUpToPowerOf2(capacity);
            m_items = new QueueSlot<T>[capacity];
            m_mask = capacity - 1;

            Clear();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MPSCQueue{T}"/> class that contains elements copied
        /// from the specified collection.
        /// Capacity will be set to a power of 2.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new <see cref="MPSCQueue{T}"/>.
        /// </param>
        public MPSCQueue(ICollection<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            var capacity = RoundUpToPowerOf2(collection.Count);
            m_items = new QueueSlot<T>[capacity];
            m_mask = capacity - 1;

            Clear();
        }

        /// <summary>
        /// Removes all objects from the <see cref="MPSCQueue{T}"/>.
        /// This method is NOT thread-safe!
        /// </summary>
        public void Clear()
        {
            m_headAndTail = new HeadAndTail();

            for (int i = 0; i < m_items.Length; i++)
            {
                m_items[i].SequenceNumber = i;

                // Zero item when clearing.
                m_items[i].Item = default;
            }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="MPSCQueue{T}"/> is empty.
        /// Value becomes stale after more enqueue or dequeue operations.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                var nextHead = Volatile.Read(ref m_headAndTail.Head) + 1;

                return (Volatile.Read(ref m_headAndTail.Tail) < nextHead);
            }
        }

        /// <summary>
        /// Gets the number of elements contained in the <see cref="MPSCQueue{T}"/>.
        /// Value becomes stale after more enqueue or dequeue operations.
        /// </summary>
        public int Count
        {
            get
            {
                var head = Volatile.Read(ref m_headAndTail.Head);
                var tail = Volatile.Read(ref m_headAndTail.Tail);
                int mask = m_mask;

                if (head != tail)
                {
                    head &= mask;
                    tail &= mask;

                    return head < tail ? tail - head : m_items.Length - head + tail;
                }
                return 0;
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
            if (array is T[] szArray)
            {
                CopyTo(szArray, index);
                return;
            }

            if (array == null)
                throw new ArgumentNullException(nameof(array));

            ToArray().CopyTo(array, index);
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="MPSCQueue{T}"/>.
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
        /// Attempts to add the object at the end of the <see cref="MPSCQueue{T}"/>.
        /// Returns false if the queue is full.
        /// </summary>
        public bool TryEnqueue(T item)
        {
            SpinWait spinner = default;

            while (true)
            {
                int tail = Volatile.Read(ref m_headAndTail.Tail);
                int index = tail & m_mask;

                int seq = Volatile.Read(ref m_items[index].SequenceNumber);
                int dif = seq - tail;

                if (dif == 0)
                {
                    // Reserve the slot
                    if (Interlocked.CompareExchange(ref m_headAndTail.Tail, tail + 1, tail) == tail)
                    {
                        // Write the value and update the seq
                        m_items[index].Item = item;
                        Volatile.Write(ref m_items[index].SequenceNumber, tail + 1);
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    // Slot was full
                    return false;
                }
                // Lost the race, try again
                spinner.SpinOnce();
            }
        }

        /// <summary>
        /// Attempts to remove and return the object at the beginning of the <see cref="MPSCQueue{T}"/>.
        /// Returns false if the queue is empty.
        /// </summary>
        public bool TryDequeue(out T item)
        {
            int head = Volatile.Read(ref m_headAndTail.Head);
            int index = head & m_mask;

            int seq = Volatile.Read(ref m_items[index].SequenceNumber);
            int dif = seq - (head + 1);

            if (dif == 0)
            {
                // Update head
                Volatile.Write(ref m_headAndTail.Head, head + 1);
                item = m_items[index].Item;

                // Zero out the slot.
                m_items[head] = default;

                // Update slot after reading
                Volatile.Write(ref m_items[index].SequenceNumber, head + m_items.Length);

                return true;
            }

            item = default;
            return false;
        }

        /// <summary>
        /// Attempts to return an object from the beginning of the <see cref="MPSCQueue{T}"/> without removing it.
        /// Returns false if the queue if empty.
        /// </summary>
        public bool TryPeek(out T item)
        {
            int head = Volatile.Read(ref m_headAndTail.Head);
            int index = head & m_mask;

            int seq = Volatile.Read(ref m_items[index].SequenceNumber);
            int dif = seq - (head + 1);

            if (dif == 0)
            {
                item = m_items[index].Item;
                return true;
            }

            item = default;
            return false;
        }

        /// <summary>
        /// Copies the elements stored in the <see cref="MPSCQueue{T}"/> to a new array.
        /// Consumer-Threadsafe
        /// </summary>
        public T[] ToArray()
        {
            int count = Count;
            var arr = new T[count];

            var iterator = new Enumerator(this);

            int i = 0;
            while (iterator.MoveNext() && i < count)
                arr[i++] = iterator.Current;

            return arr;
        }

        /// <summary>
        /// Copies the <see cref="MPSCQueue{T}"/> elements to an existing <see cref="Array">Array</see>, starting at the specified array index.
        /// Consumer-Threadsafe
        /// </summary>
        /// <param name="array">The one-dimensional <see cref="Array">Array</see> that is the destination of the elements copied from the
        /// <see cref="MPSCQueue{T}"/>. The <see cref="Array">Array</see> must have zero-based indexing.</param>
        /// <param name="index">The zero-based index in array at which copying begins.</param>
        public void CopyTo(T[] array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(Array));

            if ((uint)index > array.Length)
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index was out of range. Must be non - negative and less than the size of the collection.");

            var count = Count;

            if (index > array.Length + count)
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection.Check array index and length.");

            var iterator = new Enumerator(this);

            int i = 0;
            while (iterator.MoveNext() && i < count)
                array[i++ + index] = iterator.Current;
        }

        private static int RoundUpToPowerOf2(int i)
        {
            // Based on https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            --i;
            i |= i >> 1;
            i |= i >> 2;
            i |= i >> 4;
            i |= i >> 8;
            i |= i >> 16;
            return i + 1;
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            // Enumerates over the provided MPSCRingBuffer. Enumeration counts as a READ/Consume operation.
            // The amount of items enumerated can vary depending on if the TAIL moves during enumeration.
            // The HEAD is frozen in place when the enumerator is created. This means that the maximum 
            // amount of items read is always the capacity of the queue and no more.
#pragma warning disable IDE0032
            readonly MPSCQueue<T> m_queue;
            readonly int m_headStart;
            readonly int m_mask;
            int m_index;
            T m_current;
#pragma warning restore IDE0032

            internal Enumerator(MPSCQueue<T> queue)
            {
                m_queue = queue;
                m_index = -1;
                m_current = default;
                m_headStart = Volatile.Read(ref queue.m_headAndTail.Head);
                m_mask = queue.m_mask;
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

                int head = Volatile.Read(ref m_queue.m_headAndTail.Head);
                if (m_headStart != head)
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");

                int headIndex = head + ++m_index;
                int index = headIndex & m_mask;

                int seq = Volatile.Read(ref m_queue.m_items[index].SequenceNumber);
                int dif = seq - (headIndex + 1);

                if (dif == 0)
                {
                    m_current = m_queue.m_items[index].Item;
                    return true;
                }

                m_current = default;
                return false;
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
