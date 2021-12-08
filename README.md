# LocklessQueue
A collection of concurrent collections which are not found in the .Net framework.

Available on NuGet via ```Install-Package Lockless-Queue```

The following collections are present:
- ConcurrentHashSet (based on .Net ConcurrentDictionary)
- SPSC Queue (Single Producer, Single Consumer).
- MPSC Queue (Multi Producer, Single Consumer).
- MPMC Queue (Multi Producer, Multi Consumer).

## Benchmarks
All benchmarks are run in a .Net 5.0 environment.

### Queues
ConcurrentQueue and Queue are the built-in .Net types.

The following Benchmarks execute Enqueueing 128 items, Dequeueing 128 items and finally clearing the Queue. The same methods are used for each queue where available. All Queue benchmarks are run in a single-threaded environment to demonstrate the "raw" throughput of each queue.

|             Method |       Mean |   Error |  StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|------------------- |-----------:|--------:|--------:|-------:|-------:|------:|----------:|
|          MPMCQueue | 2,353.5 ns | 2.55 ns | 2.39 ns |      - |      - |     - |         - |
| SysConcurrentQueue | 2,860.8 ns | 7.00 ns | 6.21 ns | 0.5188 | 0.0076 |     - |    4352 B |
|          MPSCQueue | 1,619.6 ns | 9.52 ns | 8.91 ns |      - |      - |     - |         - |
|          SPSCQueue |   555.9 ns | 1.39 ns | 1.23 ns |      - |      - |     - |         - |
|              Queue |   625.1 ns | 1.95 ns | 1.82 ns |      - |      - |     - |         - |

### HashSet
A comparison is made between the ConcurrentDictionary (when using a byte as value) and the ConcurrentHashSet implementation.
The following benchmarks execute TryAdd 128 items, TryRemove 128 items and finally clearing the collection. The benchmarks are run in a single-threaded environment.

|               Method |      Mean |     Error |    StdDev |  Gen 0 |  Gen 1 | Gen 2 | Allocated |
|--------------------- |----------:|----------:|----------:|-------:|-------:|------:|----------:|
| ConcurrentDictionary | 60.580 μs | 0.0796 μs | 0.0621 μs | 3.6621 | 0.0610 |     - |  30.14 KB |
|    ConcurrentHashSet |  7.990 μs | 0.0104 μs | 0.0087 μs | 0.6104 |      - |     - |      5 KB |
