using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace LocklessQueues
{
    [StructLayout(LayoutKind.Explicit, Size = 3 * CACHE_LINE_SIZE)]
    [DebuggerDisplay("Head = {Head}, Tail = {Tail}")]
    internal struct HeadAndTail
    {
        private const int CACHE_LINE_SIZE = 64;

        [FieldOffset(1 * CACHE_LINE_SIZE)]
        public int Head;

        [FieldOffset(2 * CACHE_LINE_SIZE)]
        public int Tail;
    }
}
