using LocklessQueue.Queues;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace LocklessQueuesTests
{
    public class SPSCQueueTests
    {
        [Test]
        public void ConstructorTest()
        {
            var q = new SPSCQueue<int>(10);

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(10, q.Capacity);
        }

        [Test]
        public void FromCollectionTest()
        {
            var col = Enumerable.Range(0, 10).ToList();

            var q = new SPSCQueue<int>(col);

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(10, q.Capacity);

            Assert.IsTrue(col.SequenceEqual(q));
        }

        [Test]
        public void EnqueueTest()
        {
            var q = new SPSCQueue<int>(10);

            for (int i = 0; i < 10; i++)
            {
                q.TryEnqueue(i * i);
            }

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(10, q.Capacity);

            q.Clear();

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(10, q.Capacity);
        }

        [Test]
        public void DequeueTest()
        {
            var q = new SPSCQueue<int>(10);

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
            var q = new SPSCQueue<int>(10);

            for (int i = 0; i < 10; i++)
                q.TryEnqueue((int)Math.Pow(i + 2, 2));

            for (int i = 0; i < 10; i++)
            {
                q.TryPeek(out int num);
                Assert.AreEqual(4, num);
            }

            //Verify no items are dequeued
            Assert.AreEqual(10, q.Count);
        }

        [Test]
        public void ExpandTest()
        {
            var q = new SPSCQueue<int>(16);

            QueueTestSetup.SplitQueue(q);

            //Fill buffer to capacity.
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(q.TryEnqueue(999));


            //Buffer is full, can no longer insert.
            Assert.IsFalse(q.TryEnqueue(10));
        }

        [Test]
        public void TryActionTest()
        {
            var q = new SPSCQueue<int>(16);

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
            {
                Assert.IsTrue(q.TryDequeue(out int val));
                Assert.AreEqual(999, val);
            }

            //Empty queue
            Assert.IsFalse(q.TryPeek(out int res));


        }

        [Test]
        public void ClearTest()
        {
            var q = new SPSCQueue<int>(10);

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
            var q = new SPSCQueue<int>(10);

            // Wrap tail around
            QueueTestSetup.SplitQueue(q);

            // Iterator should start from the head.
            int num = 0;
            foreach (int i in q)
            {
                Assert.AreEqual(num, i);
                num++;
            }

            // Iterated 10 items
            Assert.AreEqual(10, num);


        }

        [Test]
        public void ToArrayTest()
        {
            var q = new SPSCQueue<int>(10);
            QueueTestSetup.SplitQueue(q);

            var arr = q.ToArray();

            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(i, arr[i]);
            }
        }

        [Test]
        public void CopyToTest()
        {
            var q = new SPSCQueue<int>(10);
            QueueTestSetup.SplitQueue(q);

            var arr = new int[15];
            q.CopyTo(arr, 5);

            var num = 0;
            for (int i = 5; i < 15; i++)
            {
                Assert.AreEqual(num++, arr[i]);
            }
        }

        [Test]
        // Demonstration that this queue is SPSC
        public void ConcurrencyTest()
        {
            var q = new SPSCQueue<int>(16);
            int count = 10000;

            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < count; i++)
                {
                    int item;
                    while (!q.TryDequeue(out item)) { }

                    Assert.AreEqual(i, item);
                }
            });

            reader.Start();

            for (int i = 0; i < count; i++)
            {
                while (!q.TryEnqueue(i)) { }
            }

            reader.Join();
        }

        //[Test]
        // Demonstration that this queue isn't MPSC

        // This test is disabled, because on rare occasions (or if Count is too small) it might fail.
        // Increasing Count will increase the likelyhood of a successful test.
        public void ConcurrencyTest2()
        {
            int count = 10000;
            var q = new SPSCQueue<int>(count);


            Thread writer = new Thread(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    while (!q.TryEnqueue(i)) { }
                }
            });
            Thread writer2 = new Thread(() =>
            {
                for (int i = 0; i < count / 2; i++)
                {
                    while (!q.TryEnqueue(i)) { }
                }
            });

            writer.Start();
            writer2.Start();


            writer.Join();
            writer2.Join();

            Assert.AreNotEqual(count, q.Count);
        }
    }
}
