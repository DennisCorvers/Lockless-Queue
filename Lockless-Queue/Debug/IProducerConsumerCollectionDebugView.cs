using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace LocklessQueue.Debug
{
    internal sealed class IProducerConsumerCollectionDebugView<T>
    {
        private readonly IProducerConsumerCollection<T> _collection;

        public IProducerConsumerCollectionDebugView(IProducerConsumerCollection<T> collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public T[] Items => _collection.ToArray();
    }
}
