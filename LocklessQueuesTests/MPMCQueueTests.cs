using LocklessQueue.Queues;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading;

namespace LocklessQueuesTests
{
    public class MPMCQueueTests
    {
        [Test]
        public void ConstructorTest()
        {
            var q = new MPMCQueue<int>(10);

            Assert.AreEqual(0, q.Count);
            Assert.AreEqual(16, q.Capacity);
        }

        [Test]
        public void ConstructorFromEnumerableTest()
        {
            var enu = Enumerable.Range(0, 10);

            var q = new MPMCQueue<int>(enu);

            Assert.AreEqual(10, q.Count);
            Assert.AreEqual(16, q.Capacity);

            foreach (var val in enu)
            {
                q.TryDequeue(out int qVal);
                Assert.AreEqual(val, qVal);
            }
        }

        [Test]
        public void EnqueueTest()
        {
            var q = new MPMCQueue<int>(100);

            for (int i = 0; i < 100; i++)
            {
                q.TryEnqueue(i * i);
            }

            Assert.AreEqual(100, q.Count);

            // Capacity becomes next power of two, which is 128 for 100.
            Assert.AreEqual(128, q.Capacity);

            q.Clear();

            Assert.AreEqual(0, q.Count);
        }

        [Test]
        public void EnqueueFailedTest()
        {
            var q = new MPMCQueue<int>(16);

            for (int i = 0; i < 16; i++)
                q.TryEnqueue(i);

            Assert.AreEqual(16, q.Capacity);
            Assert.AreEqual(16, q.Count);

            Assert.IsFalse(q.TryEnqueue(123));
        }

        [Test]
        public void DequeueTest()
        {
            var q = new MPMCQueue<int>(16);

            for (int i = 0; i < 10; i++)
                q.TryEnqueue(i * i);


            for (int i = 0; i < 10; i++)
            {
                q.TryDequeue(out int num);
                Assert.AreEqual(i * i, num);
            }
        }

        [Test]
        public void TryActionTest()
        {
            var q = new MPMCQueue<int>(16);

            //Inserts 10 items.
            TestSetup.SplitQueue(q);

            //Insert 6 more to fill the queue
            for (int i = 0; i < 6; i++)
                q.TryEnqueue(999);

            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(q.TryDequeue(out int val));
                Assert.AreEqual(i, val);
            }

            //Empty 6 last items
            for (int i = 0; i < 6; i++)
                Assert.IsTrue(q.TryDequeue(out _));

            //Empty queue
            Assert.IsFalse(q.TryDequeue(out _));
        }

        [Test]
        public void ClearTest()
        {
            var q = new MPMCQueue<int>(16);

            //Inserts 10 items.
            TestSetup.SplitQueue(q);

            Assert.AreEqual(10, q.Count);
            q.Clear();
            Assert.AreEqual(0, q.Count);

            Assert.IsTrue(q.IsEmpty);
        }

        //[Test]
        ////Demonstration that this queue is SPSC
        //public void SPSCConcurrencyTest()
        //{
        //    // Must be fixed-size or else the Queue will just expand
        //    var q = new MPMCQueue<ComplexType>(16, true);
        //    int count = 10000;


        //    Thread reader = new Thread(() =>
        //    {
        //        for (int i = 0; i < count;)
        //        {
        //            if (q.TryDequeue(out ComplexType num))
        //            {
        //                Assert.IsTrue(num.Equals(new ComplexType((ushort)i)));
        //                i++;
        //            }
        //        }
        //    });

        //    reader.Start();

        //    for (int i = 0; i < count;)
        //    {
        //        if (q.TryEnqueue(new ComplexType((ushort)i)))
        //            i++;
        //    }

        //    reader.Join();
        //}

        //[Test]
        //// Demonstration that this queue is MPSC
        //public void MPSCConcurrencyTest()
        //{
        //    var q = new MPMCQueue<int>(16000, true);
        //    int count = 10000;

        //    Thread writer = new Thread(() =>
        //    {
        //        for (int i = 0; i < count / 2;)
        //            if (q.TryEnqueue(i))
        //                i++;
        //    });

        //    Thread writer2 = new Thread(() =>
        //    {
        //        for (int i = 0; i < count / 2;)
        //            if (q.TryEnqueue(i))
        //                i++;
        //    });

        //    writer.Start();
        //    writer2.Start();


        //    writer.Join();
        //    writer2.Join();

        //    Assert.AreEqual(count, q.Count);
        //}
    }
}
