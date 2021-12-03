using BenchmarkDotNet.Attributes;
using LocklessQueue.Sets;
using System.Collections.Concurrent;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class ConcurrentHashSetBenchmarks
    {
        const int COUNT = 128;

        readonly ConcurrentDictionary<long, byte> _concurrentDictionary;
        readonly ConcurrentHashSet<long> _concurrentHashSet;

        public ConcurrentHashSetBenchmarks()
        {
            _concurrentDictionary = new ConcurrentDictionary<long, byte>();
            _concurrentHashSet = new ConcurrentHashSet<long>();
        }

        [Benchmark]
        public void ConcurrentDictionary()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _concurrentDictionary.TryAdd(i * i, 0);

            for (int i = 0; i < COUNT; i++)
                _concurrentDictionary.TryRemove(i * i, out _);

            _concurrentDictionary.Clear();
        }

        [Benchmark]
        public void ConcurrentHashSet()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _concurrentHashSet.TryAdd(i * i);

            for (int i = 0; i < COUNT; i++)
                _concurrentHashSet.TryRemove(i * i);

            _concurrentHashSet.Clear();
        }
    }
}
