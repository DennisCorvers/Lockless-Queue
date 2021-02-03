# Lockless-Queue
A collection of lockless, concurrent queues.

The following queue implementations are present:
- SPSC Queue (Single Producer, Single Consumer).
- MPSC Queue (Multi Producer, Single Consumer).
- Concurrent Queue (Multi Producer, Multi Consumer).

The Concurrent Queue implementation is a **copy** of the .Net 5.0 implementation with the added option for a fixed-size queue. This queue is lockless if/when it is instantiated as a fixed-size queue.

## Benchmarks

SysConcurrentQueue and Queue are the built-in .Net types. Where possible, a fixed-size queue has been used.

The following Benchmarks execute Enqueueing 128 items, Dequeueing 128 items and finally clearing the Queue. The same methods are used for each queue where available. All Queue benchmarks are run in a single-threaded environment to demonstrate the "raw" throughput of each queue.

|             Method |       Mean |    Error |   StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|------------------- |-----------:|---------:|---------:|-------:|-------:|------:|----------:|
|    ConcurrentQueue | 2,507.0 ns |  6.52 ns |  6.10 ns |      - |      - |     - |         - |
| SysConcurrentQueue | 2,943.2 ns | 23.93 ns | 21.21 ns | 0.7782 | 0.0153 |     - |    4928 B |
|          MPSCQueue | 1,655.7 ns |  1.30 ns |  1.22 ns |      - |      - |     - |         - |
|          SPSCQueue |   512.7 ns |  3.13 ns |  2.93 ns |      - |      - |     - |         - |
|              Queue |   588.7 ns |  1.75 ns |  1.63 ns |      - |      - |     - |         - |
