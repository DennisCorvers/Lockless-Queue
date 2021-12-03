using LocklessQueue.Sets;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LocklessQueuesTests
{
    public class ConcurrentHashSetTests
    {
        [Test]
        public void ConstructorTest()
        {
            var set = new ConcurrentHashSet<int>();

            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void ConstructorFromEnumerableTest()
        {
            var enu = Enumerable.Range(0, 10);

            var set = new ConcurrentHashSet<int>(enu);

            Assert.AreEqual(10, set.Count);

            foreach (var value in enu)
                Assert.IsTrue(set.ContainsKey(value));
        }

        [Test]
        public void AddTest()
        {
            var set = new ConcurrentHashSet<int>();

            for (int i = 0; i < 100; i++)
            {
                set.TryAdd(i * i);
            }

            Assert.AreEqual(100, set.Count);

            set.Clear();

            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void DuplicateKeyTest()
        {
            var set = new ConcurrentHashSet<int>();

            for (int i = 0; i < 16; i++)
                set.TryAdd(i);

            Assert.AreEqual(16, set.Count);

            // Add duplicate key.
            Assert.IsFalse(set.TryAdd(0));
        }

        [Test]
        public void DuplicateKeyCollectionTest()
        {
            ICollection<int> set = new ConcurrentHashSet<int>();

            for (int i = 0; i < 16; i++)
                set.Add(i);

            Assert.AreEqual(16, set.Count);

            // Add duplicate key.
            Assert.Throws<ArgumentException>(() =>
            {
                set.Add(0);
            });
        }

        [Test]
        public void RemoveTest()
        {
            var set = new ConcurrentHashSet<int>();

            for (int i = 0; i < 10; i++)
                set.TryAdd(i * i);


            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(set.Remove(i * i));
            }
        }

        [Test]
        public void ClearTest()
        {
            var set = new ConcurrentHashSet<int>();
            TestSetup.PopulateHashSet(set, 16);

            Assert.AreEqual(16, set.Count);
            set.Clear();
            Assert.AreEqual(0, set.Count);

            Assert.IsTrue(set.IsEmpty);
        }

        [Test]
        public void IteratorSingleSegmentTest()
        {
            var set = new ConcurrentHashSet<int>();
            TestSetup.PopulateHashSet(set, 16);

            int amount = 0;
            foreach (int num in set)
                amount++;

            Assert.AreEqual(amount, set.Count);
        }

        [Test]
        public void ContainsOrAddTest()
        {
            var set = new ConcurrentHashSet<int>();

            Assert.AreEqual(0, set.Count);
            Assert.IsTrue(set.ContainsOrAdd(1));
            Assert.IsTrue(set.ContainsOrAdd(1));

            Assert.IsTrue(set.ContainsOrAdd(3));
            Assert.AreEqual(2, set.Count);
        }

        [Test]
        public void ToArrayTest()
        {
            var set = new ConcurrentHashSet<int>();
            for (int i = 0; i < 16; i++)
                set.TryAdd((i + i) * i);

            var arr = set.ToArray();

            Assert.AreEqual(16, arr.Length);

            for (int i = 0; i < 16; i++)
                Assert.IsTrue(arr.Contains((i + i) * i));
        }

        [Test]
        public async Task ConcurrentAddTest()
        {
            var set = new ConcurrentHashSet<int>();
            var tasks = new List<Task>(1024);

            for (int i = 0; i < 1024; i++)
            {
                int num = i * i;
                tasks.Add(Task.Run(() =>
                {
                    set.TryAdd(num);
                }));
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(1024, set.Count);
        }

        [Test]
        public async Task ConcurrentRemoveTest()
        {
            var set = new ConcurrentHashSet<int>();
            for (int i = 0; i < 1024; i++)
                set.TryAdd(i * i);

            Assert.AreEqual(1024, set.Count);
            var tasks = new List<Task>(1024);

            for (int i = 0; i < 1024; i++)
            {
                int num = i * i;
                tasks.Add(Task.Run(() =>
                {
                    set.Remove(num);
                }));
            }

            await Task.WhenAll(tasks);

            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public async Task ConcurrentAddRemoveTest()
        {
            var set = new ConcurrentHashSet<int>();
            var tasks = new List<Task>(1024 * 2);

            for (int i = 0; i < 1024; i++)
            {
                int num = i * i;

                // Force add numbers
                tasks.Add(Task.Run(() =>
                {
                    while (!set.TryAdd(num)) ;
                }));

                // Force remove numbers
                tasks.Add(Task.Run(() =>
                {
                    while (!set.Remove(num)) ;
                }));
            }

            await Task.WhenAll(tasks);
            Assert.AreEqual(0, set.Count);
        }
    }
}
