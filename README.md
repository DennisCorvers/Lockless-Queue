# Lockless-Queue
A collection of lockless, concurrent queues.

The following queue implementations are present:
- SPSC Queue (Single Producer, Single Consumer).
- MPSC Queue (Multi Producer, Single Consumer).
- Concurrent Queue (Multi Producer, Multi Consumer).

The Concurrent Queue implementation is a **copy** of the .Net 5.0 implementation with the added option for a fixed-size queue. This queue is lockless if/when it is instantiated as a fixed-size queue.
