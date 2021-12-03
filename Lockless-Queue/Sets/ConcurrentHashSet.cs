using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using LocklessQueue.Debug;

namespace LocklessQueue.Sets
{
    /// <summary>Represents a thread-safe collection of keys.</summary>
    /// <typeparam name="TKey">The type of the keys in the set.</typeparam>
    /// <remarks>
    /// All public and protected members of <see cref="ConcurrentHashSet{TKey}"/> are thread-safe and may be used
    /// concurrently from multiple threads.
    /// </remarks>
    [DebuggerTypeProxy(typeof(IHashSetDebugView<>))]
    [DebuggerDisplay("Count = {Count}")]
    public class ConcurrentHashSet<TKey> : ICollection<TKey>, IReadOnlyCollection<TKey>
        where TKey : notnull
    {
        private volatile Tables _tables;
        private readonly IEqualityComparer<TKey> _comparer;
        private readonly EqualityComparer<TKey> _defaultComparer;
        private readonly bool _growLockArray;
        private int _budget;

        private const int DefaultCapacity = 31;
        private const int MaxLockNumber = 1024;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that is empty, has the default concurrency level, has the default initial capacity, and
        /// uses the default comparer for the key type.
        /// </summary>
        public ConcurrentHashSet()
            : this(DefaultConcurrencyLevel, DefaultCapacity, growLockArray: true, null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that is empty, has the specified concurrency level and capacity, and uses the default
        /// comparer for the key type.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the
        /// <see cref="ConcurrentHashSet{TKey}"/> concurrently.</param>
        /// <param name="capacity">The initial number of elements that the <see cref="ConcurrentHashSet{TKey}"/> can contain.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
        /// <exception cref="ArgumentOutOfRangeException"> <paramref name="capacity"/> is less than 0.</exception>
        public ConcurrentHashSet(int concurrencyLevel, int capacity)
            : this(concurrencyLevel, capacity, growLockArray: false, null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that contains elements copied from the specified <see cref="IEnumerable{T}"/>, has the default concurrency
        /// level, has the default initial capacity, and uses the default comparer for the key type.
        /// </summary>
        /// <param name="collection">The <see
        /// cref="IEnumerable{T}"/> whose elements are copied to the new <see cref="ConcurrentHashSet{TKey}"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is a null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
        public ConcurrentHashSet(IEnumerable<TKey> collection)
            : this(collection, null)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that is empty, has the specified concurrency level and capacity, and uses the specified
        /// <see cref="IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="comparer">The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.</param>
        public ConcurrentHashSet(IEqualityComparer<TKey> comparer)
            : this(DefaultConcurrencyLevel, DefaultCapacity, growLockArray: true, comparer)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that contains elements copied from the specified <see cref="IEnumerable"/>, has the default concurrency
        /// level, has the default initial capacity, and uses the specified <see cref="IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="collection">The <see cref="IEnumerable{T}"/> whose elements are copied to the new <see cref="ConcurrentHashSet{TKey}"/>.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is a null reference (Nothing in Visual Basic).</exception>
        public ConcurrentHashSet(IEnumerable<TKey> collection, IEqualityComparer<TKey> comparer)
            : this(comparer)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            InitializeFromCollection(collection);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that contains elements copied from the specified <see cref="IEnumerable"/>,
        /// has the specified concurrency level, has the specified initial capacity, and uses the specified
        /// <see cref="IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="concurrencyLevel">
        /// The estimated number of threads that will update the <see cref="ConcurrentHashSet{TKey}"/> concurrently.
        /// </param>
        /// <param name="collection">The <see cref="IEnumerable{T}"/> whose elements are copied to the new
        /// <see cref="ConcurrentHashSet{TKey}"/>.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.</param>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> is a null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1.</exception>
        /// <exception cref="ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
        public ConcurrentHashSet(int concurrencyLevel, IEnumerable<TKey> collection, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, DefaultCapacity, growLockArray: false, comparer)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            InitializeFromCollection(collection);
        }

        private void InitializeFromCollection(IEnumerable<TKey> collection)
        {
            foreach (TKey key in collection)
            {
                if (key == null)
                {
                    ThrowIfKeyNull();
                }

                if (!TryAddInternal(key, null, acquireLock: false))
                {
                    ThrowOnDuplicateKey(key);
                }
            }

            if (_budget == 0)
            {
                Tables tables = _tables;
                _budget = tables._buckets.Length / tables._locks.Length;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentHashSet{TKey}"/>
        /// class that is empty, has the specified concurrency level, has the specified initial capacity, and
        /// uses the specified <see cref="IEqualityComparer{TKey}"/>.
        /// </summary>
        /// <param name="concurrencyLevel">The estimated number of threads that will update the <see cref="ConcurrentHashSet{TKey}"/> concurrently.</param>
        /// <param name="capacity">The initial number of elements that the <see cref="ConcurrentHashSet{TKey}"/> can contain.</param>
        /// <param name="comparer">The <see cref="IEqualityComparer{TKey}"/> implementation to use when comparing keys.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="concurrencyLevel"/> is less than 1. -or- <paramref name="capacity"/> is less than 0.</exception>
        public ConcurrentHashSet(int concurrencyLevel, int capacity, IEqualityComparer<TKey> comparer)
            : this(concurrencyLevel, capacity, growLockArray: false, comparer)
        {
        }

        internal ConcurrentHashSet(int concurrencyLevel, int capacity, bool growLockArray, IEqualityComparer<TKey> comparer)
        {
            if (concurrencyLevel < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(concurrencyLevel), "ConcurrencyLevel must be positive.");
            }
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must not be negative.");
            }

            // The capacity should be at least as large as the concurrency level. Otherwise, we would have locks that don't guard
            // any buckets.
            if (capacity < concurrencyLevel)
            {
                capacity = concurrencyLevel;
            }

            var locks = new object[concurrencyLevel];
            locks[0] = locks; // reuse array as the first lock object just to avoid an additional allocation
            for (int i = 1; i < locks.Length; i++)
            {
                locks[i] = new object();
            }

            var countPerLock = new int[locks.Length];
            var buckets = new Node[capacity];
            _tables = new Tables(buckets, locks, countPerLock);

            _defaultComparer = EqualityComparer<TKey>.Default;
            if (comparer != null &&
                !ReferenceEquals(comparer, _defaultComparer) && // if this is the default comparer, take the optimized path
                !ReferenceEquals(comparer, StringComparer.Ordinal)) // strings as keys are extremely common, so special-case StringComparer.Ordinal, which is the same as the default comparer
            {
                _comparer = comparer;
            }
            _growLockArray = growLockArray;
            _budget = buckets.Length / locks.Length;
        }

        /// <summary>
        /// Attempts to add the specified key and value to the <see cref="ConcurrentHashSet{TKey}"/>.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <returns>
        /// true if the key/value pair was added to the <see cref="ConcurrentHashSet{TKey}"/> successfully; otherwise, false.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="OverflowException">The <see cref="ConcurrentHashSet{TKey}"/> contains too many elements.</exception>
        public bool TryAdd(TKey key)
        {
            if (key == null)
            {
                ThrowIfKeyNull();
            }

            return TryAddInternal(key, null, acquireLock: true);
        }

        /// <summary>
        /// Attempts to remove the key from the <see cref="ConcurrentHashSet{TKey}"/>.
        /// </summary>
        /// <param name="key">The key of the element to remove and return.</param>
        /// <returns>true if an object was removed successfully; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is a null reference (Nothing in Visual Basic).</exception>
        public bool Remove(TKey key)
        {
            if (key == null)
            {
                ThrowIfKeyNull();
            }

            return TryRemoveInternal(key);
        }

        /// <summary>
        /// Removes the specified key from the hashset if it exists.
        /// </summary>
        /// <param name="key">The key to search for and remove if it exists.</param>
        private bool TryRemoveInternal(TKey key)
        {
            IEqualityComparer<TKey> comparer = _comparer;
            int hashcode = comparer is null ? key.GetHashCode() : comparer.GetHashCode(key);
            while (true)
            {
                Tables tables = _tables;
                object[] locks = tables._locks;
                ref Node bucket = ref tables.GetBucketAndLock(hashcode, out uint lockNo);

                lock (locks[lockNo])
                {
                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurrence.
                    if (tables != _tables)
                    {
                        continue;
                    }

                    Node prev = null;
                    for (Node curr = bucket; curr != null; curr = curr._next)
                    {
                        System.Diagnostics.Debug.Assert((prev is null && curr == bucket) || prev!._next == curr);

                        if (hashcode == curr._hashcode && (comparer is null ? _defaultComparer.Equals(curr._key, key) : comparer.Equals(curr._key, key)))
                        {
                            if (prev is null)
                            {
                                Volatile.Write(ref bucket, curr._next);
                            }
                            else
                            {
                                prev._next = curr._next;
                            }

                            tables._countPerLock[lockNo]--;
                            return true;
                        }
                        prev = curr;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Checks the specified key is present in the <see cref="ConcurrentHashSet{TKey}"/>.
        /// </summary>
        /// <param name="key">The key to get.</param>
        /// <returns>true if the key was found in the <see cref="ConcurrentHashSet{TKey}"/>; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is a null reference (Nothing in Visual Basic).</exception>
        public bool ContainsKey(TKey key)
        {
            if (key is null)
            {
                ThrowIfKeyNull();
            }

            // We must capture the volatile _tables field into a local variable: it is set to a new table on each table resize.
            // The Volatile.Read on the array element then ensures that we have a copy of the reference to tables._buckets[bucketNo]:
            // this protects us from reading fields ('_hashcode', '_key', and '_next') of different instances.
            Tables tables = _tables;

            IEqualityComparer<TKey> comparer = _comparer;
            if (comparer is null)
            {
                int hashcode = key.GetHashCode();
                if (typeof(TKey).IsValueType)
                {
                    for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                    {
                        if (hashcode == n._hashcode && EqualityComparer<TKey>.Default.Equals(n._key, key))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                    {
                        if (hashcode == n._hashcode && _defaultComparer.Equals(n._key, key))
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                int hashcode = comparer.GetHashCode(key);
                for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                {
                    if (hashcode == n._hashcode && comparer.Equals(n._key, key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool ContainsKeyInternal(TKey key, int hashcode)
        {
            Tables tables = _tables;

            IEqualityComparer<TKey> comparer = _comparer;
            if (comparer is null)
            {
                if (typeof(TKey).IsValueType)
                {
                    for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                    {
                        if (hashcode == n._hashcode && EqualityComparer<TKey>.Default.Equals(n._key, key))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                    {
                        if (hashcode == n._hashcode && _defaultComparer.Equals(n._key, key))
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                for (Node n = Volatile.Read(ref tables.GetBucket(hashcode)); n != null; n = n._next)
                {
                    if (hashcode == n._hashcode && comparer.Equals(n._key, key))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Removes all keys from the <see cref="ConcurrentHashSet{TKey}"/>.
        /// </summary>
        public void Clear()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                // If the hashset is already empty, then there's nothing to clear.
                if (AreAllBucketsEmpty())
                {
                    return;
                }

                Tables tables = _tables;
                var newTables = new Tables(new Node[DefaultCapacity], tables._locks, new int[tables._countPerLock.Length]);
                _tables = newTables;
                _budget = Math.Max(1, newTables._buckets.Length / newTables._locks.Length);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copies the elements of the <see cref="ICollection{T}"/> to an array of type <typeparamref name="TKey"/>,
        /// starting at the specified array index.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional array of type <typeparamref name="TKey"/> that is the destination of the <typeparamref name="TKey"/>
        /// elements copied from the <see  cref="ICollection"/>. The array must have zero-based indexing.
        /// </param>
        /// <param name="index">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is a null reference (Nothing in Visual Basic).</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is less than 0.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="index"/> is equal to or greater than the length of the <paramref name="array"/>. -or- The number of
        /// elements in the source <see cref="ICollection"/> is greater than the available space from <paramref name="index"/> to
        /// the end of the destination <paramref name="array"/>.
        /// </exception>
        void ICollection<TKey>.CopyTo(TKey[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException("Index may not be negative.");
            }

            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = 0;
                int[] countPerLock = _tables._countPerLock;
                for (int i = 0; i < countPerLock.Length && count >= 0; i++)
                {
                    count += countPerLock[i];
                }

                if (array.Length - count < index || count < 0) //"count" itself or "count + index" can overflow
                {
                    throw new ArgumentException("Target array is not large enough.");
                }

                CopyToPairs(array, index);
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>
        /// Copies the key stored in the <see cref="ConcurrentHashSet{TKey}"/> to a
        /// new array.
        /// </summary>
        /// <returns>A new array containing a snapshot of keys copied from the <see cref="ConcurrentHashSet{TKey}"/>.
        /// </returns>
        public TKey[] ToArray()
        {
            int locksAcquired = 0;
            try
            {
                AcquireAllLocks(ref locksAcquired);

                int count = 0;
                int[] countPerLock = _tables._countPerLock;
                for (int i = 0; i < countPerLock.Length; i++)
                {
                    checked
                    {
                        count += countPerLock[i];
                    }
                }

                if (count == 0)
                {
                    return Array.Empty<TKey>();
                }

                var array = new TKey[count];
                CopyToPairs(array, 0);
                return array;
            }
            finally
            {
                ReleaseLocks(0, locksAcquired);
            }
        }

        private void CopyToPairs(TKey[] array, int index)
        {
            Node[] buckets = _tables._buckets;
            for (int i = 0; i < buckets.Length; i++)
            {
                for (Node current = buckets[i]; current != null; current = current._next)
                {
                    array[index] = current._key;
                    index++; // this should never overflow, CopyToPairs is only called when there's no overflow risk
                }
            }
        }

        /// <summary>Returns an enumerator that iterates through the <see
        /// cref="ConcurrentHashSet{TKey}"/>.</summary>
        /// <returns>An enumerator for the <see cref="ConcurrentHashSet{TKey}"/>.</returns>
        /// <remarks>
        /// The enumerator returned from the hashset is safe to use concurrently with
        /// reads and writes to the hashset, however it does not represent a moment-in-time snapshot
        /// of the hashet.  The contents exposed through the enumerator may contain modifications
        /// made to the hashset after <see cref="GetEnumerator"/> was called.
        /// </remarks>
        public IEnumerator<TKey> GetEnumerator()
            => new Enumerator(this);

        /// <summary>Provides an enumerator implementation for the hashset.</summary>
        private sealed class Enumerator : IEnumerator<TKey>
        {
            private readonly ConcurrentHashSet<TKey> _hashSet;

            private Node[] _buckets;
            private Node _node;
            private int _i;
            private int _state;

            private const int StateUninitialized = 0;
            private const int StateOuterloop = 1;
            private const int StateInnerLoop = 2;
            private const int StateDone = 3;

            public Enumerator(ConcurrentHashSet<TKey> hashset)
            {
                _hashSet = hashset;
                _i = -1;
            }

            public TKey Current { get; private set; }

            object IEnumerator.Current => Current;

            public void Reset()
            {
                _buckets = null;
                _node = null;
                Current = default;
                _i = -1;
                _state = StateUninitialized;
            }

            public void Dispose() { }

            public bool MoveNext()
            {
                switch (_state)
                {
                    case StateUninitialized:
                        _buckets = _hashSet._tables._buckets;
                        _i = -1;
                        goto case StateOuterloop;

                    case StateOuterloop:
                        var buckets = _buckets;
                        System.Diagnostics.Debug.Assert(buckets != null);

                        int i = ++_i;
                        if ((uint)i < (uint)buckets.Length)
                        {
                            // The Volatile.Read ensures that we have a copy of the reference to buckets[i]:
                            // this protects us from reading fields ('_key', and '_next') of different instances.
                            _node = Volatile.Read(ref buckets[i]);
                            _state = StateInnerLoop;
                            goto case StateInnerLoop;
                        }
                        goto default;

                    case StateInnerLoop:
                        Node node = _node;
                        if (node != null)
                        {
                            Current = node._key;
                            _node = node._next;
                            return true;
                        }
                        goto case StateOuterloop;

                    default:
                        _state = StateDone;
                        return false;
                }
            }
        }

        /// <summary>
        /// Shared internal implementation for inserts and updates.
        /// If key exists, we always return false;
        /// If key doesn't exist, we always add key and return true;
        /// </summary>
        private bool TryAddInternal(TKey key, int? nullableHashcode, bool acquireLock)
        {
            IEqualityComparer<TKey> comparer = _comparer;

            System.Diagnostics.Debug.Assert(
                nullableHashcode is null ||
                (comparer is null && key.GetHashCode() == nullableHashcode) ||
                (comparer != null && comparer.GetHashCode(key) == nullableHashcode));

            int hashcode =
                nullableHashcode ??
                (comparer is null ? key.GetHashCode() : comparer.GetHashCode(key));

            while (true)
            {
                Tables tables = _tables;
                object[] locks = tables._locks;
                ref Node bucket = ref tables.GetBucketAndLock(hashcode, out uint lockNo);

                bool resizeDesired = false;
                bool lockTaken = false;
                try
                {
                    if (acquireLock)
                    {
                        Monitor.Enter(locks[lockNo], ref lockTaken);
                    }

                    // If the table just got resized, we may not be holding the right lock, and must retry.
                    // This should be a rare occurrence.
                    if (tables != _tables)
                    {
                        continue;
                    }

                    // Try to find this key in the bucket
                    Node prev = null;
                    for (Node node = bucket; node != null; node = node._next)
                    {
                        System.Diagnostics.Debug.Assert((prev is null && node == bucket) || prev!._next == node);
                        if (hashcode == node._hashcode && (comparer is null ? _defaultComparer.Equals(node._key, key) : comparer.Equals(node._key, key)))
                        {
                            return false;
                        }
                        prev = node;
                    }

                    // The key was not found in the bucket. Insert the key.
                    var resultNode = new Node(key, hashcode, bucket);
                    Volatile.Write(ref bucket, resultNode);
                    checked
                    {
                        tables._countPerLock[lockNo]++;
                    }

                    //
                    // If the number of elements guarded by this lock has exceeded the budget, resize the bucket table.
                    // It is also possible that GrowTable will increase the budget but won't resize the bucket table.
                    // That happens if the bucket table is found to be poorly utilized due to a bad hash function.
                    //
                    if (tables._countPerLock[lockNo] > _budget)
                    {
                        resizeDesired = true;
                    }
                }
                finally
                {
                    if (lockTaken)
                    {
                        Monitor.Exit(locks[lockNo]);
                    }
                }

                //
                // The fact that we got here means that we just performed an insertion. If necessary, we will grow the table.
                //
                // Concurrency notes:
                // - Notice that we are not holding any locks at when calling GrowTable. This is necessary to prevent deadlocks.
                // - As a result, it is possible that GrowTable will be called unnecessarily. But, GrowTable will obtain lock 0
                //   and then verify that the table we passed to it as the argument is still the current table.
                //
                if (resizeDesired)
                {
                    GrowTable(tables);
                }

                return true;
            }
        }

        /// <summary>
        /// Gets the <see cref="IEqualityComparer{TKey}" />
        /// that is used to determine equality of keys for the hashset.
        /// </summary>
        /// <value>
        /// The <see cref="IEqualityComparer{TKey}" /> generic interface implementation
        /// that is used to determine equality of keys for the current
        /// <see cref="ConcurrentHashSet{TKey}" /> and to provide hash values for the keys.
        /// </value>
        /// <remarks>
        /// <see cref="ConcurrentHashSet{TKey}" /> requires an equality implementation to determine
        /// whether keys are equal. You can specify an implementation of the <see cref="IEqualityComparer{TKey}" />
        /// generic interface by using a constructor that accepts a comparer parameter;
        /// if you do not specify one, the default generic equality comparer <see cref="EqualityComparer{TKey}.Default" /> is used.
        /// </remarks>
        public IEqualityComparer<TKey> Comparer => _comparer ?? _defaultComparer;

        /// <summary>
        /// Gets the number of keys contained in the <see
        /// cref="ConcurrentHashSet{TKey}"/>.
        /// </summary>
        /// <exception cref="OverflowException">The hashset contains too many
        /// elements.</exception>
        /// <value>The number of keys contained in the <see
        /// cref="ConcurrentHashSet{TKey}"/>.</value>
        /// <remarks>Count has snapshot semantics and represents the number of items in the <see
        /// cref="ConcurrentHashSet{TKey}"/>
        /// at the moment when Count was accessed.</remarks>
        public int Count
        {
            get
            {
                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    return GetCountInternal();
                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }
            }
        }

        private int GetCountInternal()
        {
            int count = 0;
            int[] countPerLocks = _tables._countPerLock;

            // Compute the count, we allow overflow
            for (int i = 0; i < countPerLocks.Length; i++)
            {
                count += countPerLocks[i];
            }

            return count;
        }

        /// <summary>
        /// Adds a key to the <see cref="ConcurrentHashSet{TKey}"/>
        /// if the key does not already exist.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <exception cref="ArgumentNullException"><paramref name="key"/> is a null reference
        /// (Nothing in Visual Basic).</exception>
        /// <exception cref="OverflowException">The hashset contains too many
        /// elements.</exception>
        /// <returns>True if the key is already present.</returns>
        public bool ContainsOrAdd(TKey key)
        {
            if (key == null)
            {
                ThrowIfKeyNull();
            }

            IEqualityComparer<TKey> comparer = _comparer;
            int hashcode = comparer is null ? key.GetHashCode() : comparer.GetHashCode(key);

            if (!ContainsKeyInternal(key, hashcode))
            {
                return TryAddInternal(key, hashcode, acquireLock: true);
            }

            return true;
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="ConcurrentHashSet{TKey}"/> is empty.
        /// </summary>
        /// <value>true if the <see cref="ConcurrentHashSet{TKey}"/> is empty; otherwise,
        /// false.</value>
        public bool IsEmpty
        {
            get
            {
                // Check if any buckets are non-empty, without acquiring any locks.
                // This fast path should generally suffice as collections are usually not empty.
                if (!AreAllBucketsEmpty())
                {
                    return false;
                }

                // We didn't see any buckets containing items, however we can't be sure
                // the collection was actually empty at any point in time as items may have been
                // added and removed while iterating over the buckets such that we never saw an
                // empty bucket, but there was always an item present in at least one bucket.
                int acquiredLocks = 0;
                try
                {
                    // Acquire all locks
                    AcquireAllLocks(ref acquiredLocks);

                    return AreAllBucketsEmpty();
                }
                finally
                {
                    // Release locks that have been acquired earlier
                    ReleaseLocks(0, acquiredLocks);
                }


            }
        }

        void ICollection<TKey>.Add(TKey key)
        {
            if (!TryAdd(key))
            {
                ThrowOnDuplicateKey(key);
            }
        }

        bool ICollection<TKey>.Contains(TKey key)
            => ContainsKey(key);

        bool ICollection<TKey>.IsReadOnly
            => false;

        bool ICollection<TKey>.Remove(TKey key) =>
            Remove(key);

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private bool AreAllBucketsEmpty()
        {
            int[] countPerLock = _tables._countPerLock;

            for (int i = 0; i < countPerLock.Length; i++)
            {
                if (countPerLock[i] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Replaces the bucket table with a larger one. To prevent multiple threads from resizing the
        /// table as a result of races, the Tables instance that holds the table of buckets deemed too
        /// small is passed in as an argument to GrowTable(). GrowTable() obtains a lock, and then checks
        /// the Tables instance has been replaced in the meantime or not.
        /// </summary>
        private void GrowTable(Tables tables)
        {
            int locksAcquired = 0;
            try
            {
                // The thread that first obtains _locks[0] will be the one doing the resize operation
                AcquireLocks(0, 1, ref locksAcquired);

                // Make sure nobody resized the table while we were waiting for lock 0:
                if (tables != _tables)
                {
                    // We assume that since the table reference is different, it was already resized (or the budget
                    // was adjusted). If we ever decide to do table shrinking, or replace the table for other reasons,
                    // we will have to revisit this logic.
                    return;
                }

                // Compute the (approx.) total size. Use an Int64 accumulation variable to avoid an overflow.
                long approxCount = 0;
                for (int i = 0; i < tables._countPerLock.Length; i++)
                {
                    approxCount += tables._countPerLock[i];
                }

                //
                // If the bucket array is too empty, double the budget instead of resizing the table
                //
                if (approxCount < tables._buckets.Length / 4)
                {
                    _budget = 2 * _budget;
                    if (_budget < 0)
                    {
                        _budget = int.MaxValue;
                    }
                    return;
                }

                // Compute the new table size. We find the smallest integer larger than twice the previous table size, and not divisible by
                // 2,3,5 or 7. We can consider a different table-sizing policy in the future.
                int newLength = 0;
                bool maximizeTableSize = false;
                try
                {
                    checked
                    {
                        // Double the size of the buckets table and add one, so that we have an odd integer.
                        newLength = tables._buckets.Length * 2 + 1;

                        // Now, we only need to check odd integers, and find the first that is not divisible
                        // by 3, 5 or 7.
                        while (newLength % 3 == 0 || newLength % 5 == 0 || newLength % 7 == 0)
                        {
                            newLength += 2;
                        }

                        System.Diagnostics.Debug.Assert(newLength % 2 != 0);

                        if (newLength > HashHelpers.MaxArraySize)
                        {
                            maximizeTableSize = true;
                        }
                    }
                }
                catch (OverflowException)
                {
                    maximizeTableSize = true;
                }

                if (maximizeTableSize)
                {
                    newLength = HashHelpers.MaxArraySize;

                    // We want to make sure that GrowTable will not be called again, since table is at the maximum size.
                    // To achieve that, we set the budget to int.MaxValue.
                    //
                    // (There is one special case that would allow GrowTable() to be called in the future:
                    // calling Clear() on the ConcurrentHashSet will shrink the table and lower the budget.)
                    _budget = int.MaxValue;
                }

                object[] newLocks = tables._locks;

                // Add more locks
                if (_growLockArray && tables._locks.Length < MaxLockNumber)
                {
                    newLocks = new object[tables._locks.Length * 2];
                    Array.Copy(tables._locks, newLocks, tables._locks.Length);
                    for (int i = tables._locks.Length; i < newLocks.Length; i++)
                    {
                        newLocks[i] = new object();
                    }
                }

                var newBuckets = new Node[newLength];
                var newCountPerLock = new int[newLocks.Length];
                var newTables = new Tables(newBuckets, newLocks, newCountPerLock);

                // Now acquire all other locks for the table
                AcquireLocks(1, tables._locks.Length, ref locksAcquired);

                // Copy all data into a new table, creating new nodes for all elements
                foreach (Node bucket in tables._buckets)
                {
                    Node current = bucket;
                    while (current != null)
                    {
                        Node next = current._next;
                        ref Node newBucket = ref newTables.GetBucketAndLock(current._hashcode, out uint newLockNo);

                        newBucket = new Node(current._key, current._hashcode, newBucket);

                        checked
                        {
                            newCountPerLock[newLockNo]++;
                        }

                        current = next;
                    }
                }

                // Adjust the budget
                _budget = Math.Max(1, newBuckets.Length / newLocks.Length);

                // Replace tables with the new versions
                _tables = newTables;
            }
            finally
            {
                // Release all locks that we took earlier
                ReleaseLocks(0, locksAcquired);
            }
        }

        /// <summary>The number of concurrent writes for which to optimize by default.</summary>
        private static int DefaultConcurrencyLevel => Environment.ProcessorCount;

        /// <summary>
        /// Acquires all locks for this hash table, and increments locksAcquired by the number
        /// of locks that were successfully acquired. The locks are acquired in an increasing
        /// order.
        /// </summary>
        private void AcquireAllLocks(ref int locksAcquired)
        {
            // First, acquire lock 0
            AcquireLocks(0, 1, ref locksAcquired);

            // Now that we have lock 0, the _locks array will not change (i.e., grow),
            // and so we can safely read _locks.Length.
            AcquireLocks(1, _tables._locks.Length, ref locksAcquired);
            System.Diagnostics.Debug.Assert(locksAcquired == _tables._locks.Length);
        }

        /// <summary>
        /// Acquires a contiguous range of locks for this hash table, and increments locksAcquired
        /// by the number of locks that were successfully acquired. The locks are acquired in an
        /// increasing order.
        /// </summary>
        private void AcquireLocks(int fromInclusive, int toExclusive, ref int locksAcquired)
        {
            System.Diagnostics.Debug.Assert(fromInclusive <= toExclusive);
            object[] locks = _tables._locks;

            for (int i = fromInclusive; i < toExclusive; i++)
            {
                bool lockTaken = false;
                try
                {
                    Monitor.Enter(locks[i], ref lockTaken);
                }
                finally
                {
                    if (lockTaken)
                    {
                        locksAcquired++;
                    }
                }
            }
        }

        /// <summary>
        /// Releases a contiguous range of locks.
        /// </summary>
        private void ReleaseLocks(int fromInclusive, int toExclusive)
        {
            System.Diagnostics.Debug.Assert(fromInclusive <= toExclusive);

            Tables tables = _tables;
            for (int i = fromInclusive; i < toExclusive; i++)
            {
                Monitor.Exit(tables._locks[i]);
            }
        }

        private static void ThrowIfKeyNull()
            => throw new ArgumentNullException("Key may not be null.");

        private static void ThrowOnDuplicateKey(TKey key)
            => throw new ArgumentException($"An item with the same key has already been added. Key: {key}");

        /// <summary>
        /// A node in a singly-linked list representing a particular hash table bucket.
        /// </summary>
        private sealed class Node
        {
            internal readonly TKey _key;
            internal volatile Node _next;
            internal readonly int _hashcode;

            internal Node(TKey key, int hashcode, Node next)
            {
                _key = key;
                _next = next;
                _hashcode = hashcode;
            }
        }

        /// <summary>Tables that hold the internal state of the ConcurrentHashSet</summary>
        /// <remarks>
        /// Wrapping the three tables in a single object allows us to atomically
        /// replace all tables at once.
        /// </remarks>
        private sealed class Tables
        {
            /// <summary>A singly-linked list for each bucket.</summary>
            internal readonly Node[] _buckets;
            /// <summary>A set of locks, each guarding a section of the table.</summary>
            internal readonly object[] _locks;
            /// <summary>The number of elements guarded by each lock.</summary>
            internal readonly int[] _countPerLock;
            /// <summary>Pre-computed multiplier for use on 64-bit performing faster modulo operations.</summary>
            internal readonly ulong _fastModBucketsMultiplier;

            internal Tables(Node[] buckets, object[] locks, int[] countPerLock)
            {
                _buckets = buckets;
                _locks = locks;
                _countPerLock = countPerLock;
                if (IntPtr.Size == 8)
                {
                    _fastModBucketsMultiplier = HashHelpers.GetFastModMultiplier((uint)buckets.Length);
                }
            }

            /// <summary>Computes a ref to the bucket for a particular key.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Node GetBucket(int hashcode)
            {
                Node[] buckets = _buckets;
                if (IntPtr.Size == 8)
                {
                    return ref buckets[HashHelpers.FastMod((uint)hashcode, (uint)buckets.Length, _fastModBucketsMultiplier)];
                }
                else
                {
                    return ref buckets[(uint)hashcode % (uint)buckets.Length];
                }
            }

            /// <summary>Computes the bucket and lock number for a particular key.</summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal ref Node GetBucketAndLock(int hashcode, out uint lockNo)
            {
                Node[] buckets = _buckets;
                uint bucketNo;
                if (IntPtr.Size == 8)
                {
                    bucketNo = HashHelpers.FastMod((uint)hashcode, (uint)buckets.Length, _fastModBucketsMultiplier);
                }
                else
                {
                    bucketNo = (uint)hashcode % (uint)buckets.Length;
                }
                lockNo = bucketNo % (uint)_locks.Length; // doesn't use FastMod, as it would require maintaining a different multiplier
                return ref buckets[bucketNo];
            }
        }
    }
}
