using LocklessQueue.Queues;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace LocklessQueuesTests
{
    public class MPSCQueueTests
    {
        [Test]
        public void ConstructorTest()
        {
            var q = new MPSCQueue<int>(10);

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(16, q.Capacity);
        }

        [Test]
        public void FromCollectionTest()
        {
            var col = Enumerable.Range(0, 10).ToList();

            var q = new MPSCQueue<int>(col);

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(16, q.Capacity);

            Assert.IsTrue(col.SequenceEqual(q));
        }

        [Test]
        public void EnqueueTest()
        {
            var q = new MPSCQueue<int>(10);

            for (int i = 0; i < 10; i++)
            {
                q.TryEnqueue(i * i);
            }

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(16, q.Capacity);

            q.Clear();

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(16, q.Capacity);
        }

        [Test]
        public void DequeueTest()
        {
            var q = new MPSCQueue<int>(10);

            for (int i = 0; i < 10; i++)
                q.TryEnqueue(i * i);


            for (int i = 0; i < 10; i++)
            {
                q.TryDequeue(out int num);
                Assert.AreEqual(i * i, num);
            }
        }

        [Test]
        public void PeekTest()
        {
            var q = new MPSCQueue<int>(10);

            for (int i = 0; i < 10; i++)
                q.TryEnqueue((int)Math.Pow(i + 2, 2));

            for (int i = 0; i < 10; i++)
            {
                q.TryPeek(out int result);
                Assert.AreEqual(4, result);
            }

            //Verify no items are dequeued
            Assert.AreEqual(10, q.Count);
        }

        [Test]
        public void ExpandTest()
        {
            var q = new MPSCQueue<int>(10);

            QueueTestSetup.SplitQueue(q);

            //Fill buffer to capacity.
            for (int i = 0; i < 6; i++)
                q.TryEnqueue(999);


            //Buffer is full, can no longer insert.
            Assert.IsFalse(q.TryEnqueue(10));
        }

        [Test]
        public void TryActionTest()
        {
            var q = new MPSCQueue<int>(16);

            //Inserts 10 items.
            QueueTestSetup.SplitQueue(q);

            //Insert 6 more to fill the queue
            for (int i = 0; i < 6; i++)
                q.TryEnqueue(999);

            Assert.IsFalse(q.TryEnqueue(10));
            Assert.IsTrue(q.TryPeek(out int result));
            Assert.AreEqual(0, result);

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(q.TryDequeue(out int val));
                Assert.AreEqual(i, val);
            }

            //Empty 6 last items
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(q.TryDequeue(out int val));

            //Empty queue
            Assert.IsFalse(q.TryPeek(out int res));
        }

        [Test]
        public void ClearTest()
        {
            var q = new MPSCQueue<int>(16);

            //Inserts 10 items.
            QueueTestSetup.SplitQueue(q);

            Assert.AreEqual(10, q.Count);
            q.Clear();
            Assert.AreEqual(0, q.Count);

            Assert.IsTrue(q.IsEmpty);
        }

        [Test]
        public void IteratorTest()
        {
            var q = new MPSCQueue<int>(10);

            // Wrap tail around
            QueueTestSetup.SplitQueue(q);

            // Iterator should start from the head.
            int num = 0;
            var iterator = q.GetEnumerator();
            while (iterator.MoveNext())
            {
                Assert.AreEqual(num, iterator.Current);
                num++;
            }

            // Iterated 10 items
            Assert.AreEqual(10, num);
        }

        [Test]
        public void CopyToTest()
        {
            var q = new MPSCQueue<int>(10);
            QueueTestSetup.SplitQueue(q);

            var arr = new int[q.Count];
            q.CopyTo(arr, 0);

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i, arr[i]);
            }
        }

        [Test]
        public void ConcurrentIteratorTest()
        {
            int count = 1000;
            var q = new MPSCQueue<int>(count);

            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    while (!q.TryEnqueue(i)) ;
                }
            });

            reader.Start();

            // Wait so we have some data to copy.
            Thread.Sleep(1);

            var num = 0;
            foreach (int i in q)
            {
                Assert.AreEqual(num++, i);
            }

            reader.Join();
        }

        [Test]
        //Demonstration that this queue is SPSC
        public void SPSCConcurrencyTest()
        {
            var q = new MPSCQueue<ComplexType>(16);
            int count = 10000;


            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    ComplexType val;
                    while (!q.TryDequeue(out val)) ;

                    Assert.IsTrue(val.Equals(new ComplexType((ushort)i)));
                }
            });

            reader.Start();

            for (int i = 0; i < count; i++)
            {
                var obj = new ComplexType((ushort)i);
                while (!q.TryEnqueue(obj)) ;
            }

            reader.Join();
        }

        [Test]
        // Demonstration that this queue is MPSC
        public void MPSCConcurrencyTest()
        {
            var q = new MPSCQueue<int>(16000);
            int count = 10000;

            Thread writer = new Thread(() =>
            {
                for (int i = 0; i < count / 2; i++)
                    while (!q.TryEnqueue(i)) ;
            });

            Thread writer2 = new Thread(() =>
            {
                for (int i = 0; i < count / 2; i++)
                    while (!q.TryEnqueue(i)) ;
            });

            writer.Start();
            writer2.Start();


            writer.Join();
            writer2.Join();

            Assert.AreEqual(count, q.Count);
        }
    }
}
