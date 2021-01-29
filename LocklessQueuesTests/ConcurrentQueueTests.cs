using LocklessQueues;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace LocklessQueuesTests
{
    public class ConcurrentQueueTests
    {
        [Test]
        public void ConstructorFixedTest()
        {
            var q = new ConcurrentQueue<int>(10, true);

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(16, q.Capacity);
            Assert.IsTrue(q.IsFixedSize);
        }

        [Test]
        public void ConstructorTest()
        {
            var q = new ConcurrentQueue<int>();

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(32, q.Capacity);
            Assert.IsFalse(q.IsFixedSize);
        }

        [Test]
        public void ConstructorFromEnumerableTest()
        {
            var enu = Enumerable.Range(0, 10);

            var q = new ConcurrentQueue<int>(enu);

            Assert.IsFalse(q.IsFixedSize);
            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(32, q.Capacity);

            Enumerable.SequenceEqual(q, enu);
        }

        [Test]
        public void EnqueueFixedTest()
        {
            var q = new ConcurrentQueue<int>(100, true);

            for (int i = 0; i < 100; i++)
            {
                q.TryEnqueue(i * i);
            }

            Assert.AreEqual(100, q.Count);

            // Capacity becomes next power of two, which is 128 for 100.
            Assert.AreEqual(128, q.Capacity);
            Assert.IsTrue(q.IsFixedSize);

            q.Clear();

            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void EnqueueTest()
        {
            var q = new ConcurrentQueue<int>();

            for (int i = 0; i < 100; i++)
            {
                q.TryEnqueue(i * i);
            }

            Assert.AreEqual(100, q.Count);
            Assert.IsFalse(q.IsFixedSize);

            // Capacity is expected to be somewhere greater than 100.
            Assert.Greater(q.Capacity, 100);

            q.Clear();

            Assert.AreEqual(0, q.Count);

            // Capacity is expected to be the default segment size
            Assert.AreEqual(q.Capacity, 32);
        }

        [Test]
        public void EnqueueFailedTest()
        {
            var q = new ConcurrentQueue<int>(16, true);

            for (int i = 0; i < 16; i++)
                q.Enqueue(i);

            Assert.AreEqual(16, q.Capacity);
            Assert.AreEqual(16, q.Count);
            Assert.IsTrue(q.IsFixedSize);

            // Fixed-size queue is full. Next Enqueue should throw.
            Assert.Catch<InvalidOperationException>(() =>
            {
                q.Enqueue(123);
            });
        }

        [Test]
        public void DequeueTest()
        {
            var q = new ConcurrentQueue<int>();

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
            var q = new ConcurrentQueue<int>();

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
            var q = new ConcurrentQueue<int>(16, false);

            QueueTestSetup.SplitQueue(q);

            // Fill buffer beyond capacity
            for (int i = 0; i < 100;)
            {
                if (q.TryEnqueue(999))
                    i++;
            }


            Assert.AreEqual(110, q.Count);
        }

        [Test]
        public void ExpandFixedTest()
        {
            var q = new ConcurrentQueue<int>(64, true);

            // Fill buffer to capacity
            for (int i = 0; i < 64;)
            {
                if (q.TryEnqueue(i))
                    i++;
            }


            // Buffer is full, can no longer insert.
            Assert.IsFalse(q.TryEnqueue(10));
        }

        [Test]
        public void TryActionTest()
        {
            var q = new ConcurrentQueue<int>(16, true);

            //Inserts 10 items.
            QueueTestSetup.SplitQueue(q);

            //Insert 6 more to fill the queue
            for (int i = 0; i < 6; i++)
                q.TryEnqueue(999);

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
            var q = new ConcurrentQueue<int>();

            //Inserts 10 items.
            QueueTestSetup.SplitQueue(q);

            Assert.AreEqual(10, q.Count);
            q.Clear();
            Assert.AreEqual(0, q.Count);

            Assert.IsTrue(q.IsEmpty);
        }

        [Test]
        public void ClearFixedTest()
        {
            int size = 128; // Power of two.
            var q = new ConcurrentQueue<int>(size, true);

            //Inserts 10 items.
            QueueTestSetup.SplitQueue(q);

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(size, q.Capacity);

            q.Clear();

            Assert.AreEqual(0, q.Count);
            // Queue capacity needs to remain unchanged after clear.
            Assert.AreEqual(size, q.Capacity);

            Assert.IsTrue(q.IsEmpty);
        }

        [Test]
        public void IteratorSingleSegmentTest()
        {
            var q = new ConcurrentQueue<int>();

            for (int i = 0; i < 10; i++)
                q.Enqueue(i);

            int ii = 0;
            foreach (int num in q)
            {
                Assert.AreEqual(ii, num);
                ii++;
            }

            Assert.AreEqual(10, ii);
        }

        [Test]
        public void IteratorMultiSegmentTest()
        {
            var q = new ConcurrentQueue<int>();

            // Enqueue large amount so we get multiple segments.
            for (int i = 0; i < 50; i++)
                q.TryEnqueue(i);

            int ii = 0;
            foreach (int num in q)
            {
                Assert.AreEqual(ii, num);
                ii++;
            }

            Assert.AreEqual(50, ii);
        }

        [Test]
        public void IteratorSplitTest()
        {
            var q = new ConcurrentQueue<int>(10, false);

            // Wrap tail around
            QueueTestSetup.SplitQueue(q);

            for (int i = 10; i < 50; i++)
                q.Enqueue(i);

            // Iterator should start from the head.
            int num = 0;
            foreach (int i in q)
            {
                Assert.AreEqual(num, i);
                num++;
            }

            // Iterated 50 items
            Assert.AreEqual(50, num);

        }

        [Test]
        public void IteratorConcurrencyTest()
        {
            var q = new ConcurrentQueue<int>(16000, true);
            int count = 10000;

            Thread writer = new Thread(() =>
            {
                for (int i = 0; i < count;)
                    if (q.TryEnqueue(i))
                        i++;
            });

            writer.Start();

            Thread.Sleep(3); // Wait some arbitrary time so there's data to enumerate

            int num = 0;
            foreach (int i in q)
                Assert.AreEqual(num++, i);

            writer.Join();

            Assert.AreEqual(count, q.Count);
        }

        [Test]
        //Demonstration that this queue is SPSC
        public void SPSCConcurrencyTest()
        {
            // Must be fixed-size or else the Queue will just expand
            var q = new ConcurrentQueue<ComplexType>(16, true);
            int count = 10000;


            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < count;)
                {
                    if (q.TryDequeue(out ComplexType num))
                    {
                        Assert.IsTrue(num.Equals(new ComplexType((ushort)i)));
                        i++;
                    }
                }
            });

            reader.Start();

            for (int i = 0; i < count;)
            {
                if (q.TryEnqueue(new ComplexType((ushort)i)))
                    i++;
            }

            reader.Join();
        }

        [Test]
        // Demonstration that this queue is MPSC
        public void MPSCConcurrencyTest()
        {
            var q = new ConcurrentQueue<int>(16000, true);
            int count = 10000;

            Thread writer = new Thread(() =>
            {
                for (int i = 0; i < count / 2;)
                    if (q.TryEnqueue(i))
                        i++;
            });

            Thread writer2 = new Thread(() =>
            {
                for (int i = 0; i < count / 2;)
                    if (q.TryEnqueue(i))
                        i++;
            });

            writer.Start();
            writer2.Start();


            writer.Join();
            writer2.Join();

            Assert.AreEqual(count, q.Count);
        }
    }
}
