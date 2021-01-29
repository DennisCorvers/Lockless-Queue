using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace LocklessQueuesTests
{
    internal class QueueTestSetup
    {
        internal static void SplitQueue(IProducerConsumerCollection<int> q)
        {
            //Wrap tail back to 0
            for (int i = 0; i < 5; i++)
                q.TryAdd(111);

            //First half
            for (int i = 0; i < 5; i++)
                q.TryAdd(i);

            //Move head by 5
            for (int i = 0; i < 5; i++)
                q.TryTake(out int _);

            //Second half (head and tail are now both 5)
            for (int i = 5; i < 10; i++)
                q.TryAdd(i);

            //Circular buffer now "ends" in the middle of the underlying array

            if (q.Count < 10)
                throw new ArgumentException("Queue needs to have a capacity of at least 10.");
        }
    }
}
