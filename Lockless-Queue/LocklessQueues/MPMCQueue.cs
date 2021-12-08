using LocklessQueue.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LocklessQueue.Queues
{
    /// <summary>
    /// Represents a thread-safe first-in, first-out collection of objects.
    /// </summary>
    /// <typeparam name="T">Specifies the type of elements in the queue.</typeparam>
    /// <remarks>
    /// All public and protected members of <see cref="MPMCQueue{T}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
    public class MPMCQueue<T>
    {
        private QueueSlot<T>[] m_items;
        private HeadAndTail m_headAndTail;
        private readonly int m_mask;

        /// <summary>
        /// Initializes a new instance of the <see cref="MPSCQueue{T}"/> class. Capacity will be set to a power of 2.
        /// </summary>
        /// <param name="capacity">The fixed-capacity of this <see cref="MPSCQueue{T}"/></param>
        public MPMCQueue(int capacity)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");

            capacity = MathUtils.RoundUpToPowerOf2(capacity);
            m_items = new QueueSlot<T>[capacity];
            m_mask = capacity - 1;

            ClearFast();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MPMCQueue{T}"/> class that contains elements copied
        /// from the specified collection.
        /// Capacity will be set to a power of 2.
        /// </summary>
        /// <param name="collection">
        /// The collection whose elements are copied to the new <see cref="MPMCQueue{T}"/>.
        /// </param>
        public MPMCQueue(IEnumerable<T> collection)
        {
            if (collection == null)
                throw new ArgumentNullException(nameof(collection));

            var capacity = MathUtils.RoundUpToPowerOf2(collection.Count());
            m_items = new QueueSlot<T>[capacity];
            m_mask = capacity - 1;

            var index = 0;
            foreach (var item in collection)
            {
                var tail = m_headAndTail.Tail;

                // Increment tail.
                m_headAndTail.Tail = tail + 1;

                // Fill slot
                m_items[index].Item = item;
                m_items[index].SequenceNumber = tail + 1;

                index++;
            }

            // Fill remaining sequence numbers.
            for (; index < capacity; index++)
                m_items[index].SequenceNumber = index;
        }

        private void ClearFast()
        {
            m_headAndTail = new HeadAndTail();

            for (int i = 0; i < m_items.Length; i++)
            {
                m_items[i].SequenceNumber = i;
                m_items[i].Item = default;
            }
        }

        /// <summary>
        /// Empties the <see cref="MPMCQueue{T}"/> by dequeueing all remaining items.
        /// This might include new enqueue operations.
        /// </summary>
        public void Clear()
            => ClearSlow();

        private void ClearSlow()
        {
            while (TryDequeue(out _)) ;
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="MPMCQueue{T}"/> is empty.
        /// </summary>
        /// <value>true if the <see cref="MPMCQueue{T}"/> is empty; otherwise, false.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of this property is recommended
        /// rather than retrieving the number of items from the <see cref="Count"/> property and comparing it
        /// to 0.  However, as this collection is intended to be accessed concurrently, it may be the case
        /// that another thread will modify the collection after <see cref="IsEmpty"/> returns, thus invalidating
        /// the result.
        /// </remarks>
        public bool IsEmpty
            => !HasNextItem();

        /// <summary>
        /// Gets the number of elements contained in the <see cref="MPMCQueue{T}"/>.
        /// </summary>
        /// <value>The number of elements contained in the <see cref="MPMCQueue{T}"/>.</value>
        /// <remarks>
        /// For determining whether the collection contains any items, use of the <see cref="IsEmpty"/>
        /// property is recommended rather than retrieving the number of items from the <see cref="Count"/>
        /// property and comparing it to 0.
        /// </remarks>
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

        /// <summary>
        /// Gets the maximum number of elements that can be stored in the <see cref="MPMCQueue{T}"/>.
        /// </summary>
        public int Capacity
            => m_items.Length;

        /// <summary>
        /// Attempts to add the object at the end of the <see cref="MPMCQueue{T}"/>.
        /// </summary>
        /// <returns>
        /// true if an element was added to the end of the <see cref="MPMCQueue{T}"/> successfully; otherwise, false.
        /// </returns>
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
        /// Attempts to remove and return the object at the beginning of the <see
        /// cref="MPMCQueue{T}"/>.
        /// </summary>
        /// <param name="result">
        /// When this method returns, if the operation was successful, <paramref name="result"/> contains the
        /// object removed. If no object was available to be removed, the value is unspecified.
        /// </param>
        /// <returns>
        /// true if an element was removed and returned from the beginning of the
        /// <see cref="MPMCQueue{T}"/> successfully; otherwise, false.
        /// </returns>
        public bool TryDequeue(out T result)
        {
            SpinWait spinner = default;

            while (true)
            {
                int head = Volatile.Read(ref m_headAndTail.Head);
                int index = head & m_mask;

                int seq = Volatile.Read(ref m_items[index].SequenceNumber);
                int dif = seq - (head + 1);

                if (dif == 0)
                {
                    // Reserve the slot
                    if (Interlocked.CompareExchange(ref m_headAndTail.Head, head + 1, head) == head)
                    {
                        // Retrieve the value, clear the slot, and update the seq
                        result = m_items[index].Item;
                        m_items[index].Item = default;
                        Volatile.Write(ref m_items[index].SequenceNumber, head + m_items.Length);
                        return true;
                    }
                }
                else if (dif < 0)
                {
                    int tail = Volatile.Read(ref m_headAndTail.Tail);
                    if (tail - head <= 0)
                    {
                        result = default;
                        return false;
                    }
                }

                // Lost the race, try again
                spinner.SpinOnce();
            }
        }

        private bool HasNextItem()
        {
            // Quickly checks if a next item is available (used in IsEmpty).
            // Does not reserve a slot that would block Dequeue ops.
            SpinWait spinner = default;

            while (true)
            {
                int head = Volatile.Read(ref m_headAndTail.Head);
                int index = head & m_mask;

                int seq = Volatile.Read(ref m_items[index].SequenceNumber);
                int dif = seq - (head + 1);

                if (dif == 0)
                {
                    return true;
                }
                else if (dif < 0)
                {
                    int tail = Volatile.Read(ref m_headAndTail.Tail);
                    if (tail - head <= 0)
                    {
                        return false;
                    }
                }

                spinner.SpinOnce();
            }
        }
    }
}
