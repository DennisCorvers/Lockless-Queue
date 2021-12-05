# LocklessQueue
A collection of concurrent collections that are not found in the .Net framework.

Available on NuGet via ```Install-Package Lockless-Queue```

The following collections are present:
- ConcurrentHashSet (based on .Net ConcurrentDictionary)
- SPSC Queue (Single Producer, Single Consumer).
- MPSC Queue (Multi Producer, Single Consumer).
- Concurrent Queue (Multi Producer, Multi Consumer).

The Concurrent Queue implementation is a **copy** of the .Net 5.0 implementation with the added option for a fixed-size queue. This queue is lockless if/when it is instantiated as a fixed-size queue.

## Benchmarks
All benchmarks are run in a .Net 5.0 environment.

### Queues
SysConcurrentQueue and Queue are the built-in .Net types. Where possible, a fixed-size queue has been used.

The following Benchmarks execute Enqueueing 128 items, Dequeueing 128 items and finally clearing the Queue. The same methods are used for each queue where available. All Queue benchmarks are run in a single-threaded environment to demonstrate the "raw" throughput of each queue.

|             Method |       Mean |    Error |   StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|------------------- |-----------:|---------:|---------:|-------:|-------:|------:|----------:|
|    ConcurrentQueue | 2,473.3 ns |  2.27 ns |  2.01 ns |      - |      - |     - |         - |
| SysConcurrentQueue | 2,860.0 ns | 16.30 ns | 15.24 ns | 0.5188 | 0.0076 |     - |    4352 B |
|          MPSCQueue | 1,612.5 ns |  1.87 ns |  1.75 ns |      - |      - |     - |         - |
|          SPSCQueue |   562.1 ns |  4.05 ns |  3.78 ns |      - |      - |     - |         - |
|              Queue |   612.5 ns |  1.33 ns |  1.24 ns |      - |      - |     - |         - |

### HashSet
A comparison is made between the ConcurrentDictionary (when using a byte as value) and the ConcurrentHashSet implementation.
The following benchmarks execute TryAdd 128 items, TryRemove 128 items and finally clearing the collection. The benchmarks are run in a single-threaded environment.

|               Method |      Mean |     Error |    StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|--------------------- |----------:|----------:|----------:|-------:|-------:|------:|----------:|
| ConcurrentDictionary | 60.580 μs | 0.0796 μs | 0.0621 μs | 3.6621 | 0.0610 |     - |  30.14 KB |
|    ConcurrentHashSet |  7.990 μs | 0.0104 μs | 0.0087 μs | 0.6104 |      - |     - |      5 KB |
