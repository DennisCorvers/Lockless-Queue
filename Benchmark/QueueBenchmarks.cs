﻿using BenchmarkDotNet.Attributes;
using LocklessQueue.Queues;
using System.Collections.Generic;
using SysConcurrentQueue = System.Collections.Concurrent.ConcurrentQueue<long>;

namespace Benchmark
{
    [MemoryDiagnoser]
    public class QueueBenchmarks
    {
        const int COUNT = 128;

        readonly MPMCQueue<long> _mpmcQueue;
        readonly SysConcurrentQueue _systemConcurrentQueue;
        readonly MPSCQueue<long> _mpscQueue;
        readonly SPSCQueue<long> _spscQueue;
        readonly Queue<long> _queue;

        public QueueBenchmarks()
        {
            _mpmcQueue = new MPMCQueue<long>(COUNT);
            _systemConcurrentQueue = new SysConcurrentQueue();
            _mpscQueue = new MPSCQueue<long>(COUNT);
            _spscQueue = new SPSCQueue<long>(COUNT);
            _queue = new Queue<long>(COUNT);
        }

        [Benchmark]
        public void MPMCQueue()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _mpmcQueue.TryEnqueue(i);

            for (int i = 0; i < COUNT; i++)
                _mpmcQueue.TryDequeue(out long result);

            _mpmcQueue.Clear();
        }

        [Benchmark]
        public void SysConcurrentQueue()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _systemConcurrentQueue.Enqueue(i);

            for (int i = 0; i < COUNT; i++)
                _systemConcurrentQueue.TryDequeue(out long result);

            _systemConcurrentQueue.Clear();
        }

        [Benchmark]
        public void MPSCQueue()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _mpscQueue.TryEnqueue(i);

            for (int i = 0; i < COUNT; i++)
                _mpscQueue.TryDequeue(out long result);

            _mpscQueue.Clear();
        }

        [Benchmark]
        public void SPSCQueue()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _spscQueue.TryEnqueue(i);

            for (int i = 0; i < COUNT; i++)
                _spscQueue.TryDequeue(out long result);

            _spscQueue.Clear();
        }

        [Benchmark]
        public void Queue()
        {
            // ADD values
            for (int i = 0; i < COUNT; i++)
                _queue.Enqueue(i);

            for (int i = 0; i < COUNT; i++)
                _queue.TryDequeue(out long result);

            _queue.Clear();
        }
    }
}
