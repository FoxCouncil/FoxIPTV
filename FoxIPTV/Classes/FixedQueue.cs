// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Classes
{
    using System.Collections.Concurrent;

    class FixedQueue<T> : ConcurrentQueue<T>
    {
        /// <summary>One hundred elements ought to be enough for anyone.</summary>
        private const int DefaultFixedSize = 100;

        /// <summary>The dequeue lock system</summary>
        private readonly object _fixedQueueLock = new object();

        /// <summary>Gets or sets the value that determines the fixed size of this <see cref="ConcurrentQueue{T}"/></summary>
        public int FixedSize { get; set; } = DefaultFixedSize;

        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);

            lock (_fixedQueueLock)
            {
                while (Count > FixedSize && TryDequeue(out var tempObj)) { }
            }
        }
    }
}
