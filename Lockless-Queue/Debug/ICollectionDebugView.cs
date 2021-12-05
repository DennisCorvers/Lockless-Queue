using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace LocklessQueue.Debug
{
    internal sealed class ICollectionDebugView<TKey>
        where TKey : notnull
    {
        private readonly ICollection<TKey> m_collection;

        public ICollectionDebugView(ICollection<TKey> hashset)
        {
            m_collection = hashset ?? throw new ArgumentNullException(nameof(hashset));
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public TKey[] Items
        {
            get
            {
                var items = new TKey[m_collection.Count];
                m_collection.CopyTo(items, 0);
                return items;
            }
        }
    }
}
