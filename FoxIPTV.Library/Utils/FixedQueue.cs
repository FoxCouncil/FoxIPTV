// Copyright (c) 2019 Fox Council - MIT License - https://github.com/FoxCouncil/FoxIPTV

namespace FoxIPTV.Library.Utils
{
    using System.Collections.Concurrent;

    /// <summary>A fixed length <see cref="ConcurrentQueue"/>, discarding older contents when FixedSize is reached</summary>
    /// <typeparam name="T">A generic type to queue</typeparam>
    class FixedQueue<T> : ConcurrentQueue<T>
    {
        /// <summary>One hundred elements ought to be enough for anyone.</summary>
        private const int DefaultFixedSize = 100;

        /// <summary>The dequeue lock system</summary>
        private readonly object _fixedQueueLock = new object();

        /// <summary>Gets or sets the value that determines the fixed size of this <see cref="ConcurrentQueue{T}"/></summary>
        public int FixedSize { get; set; } = DefaultFixedSize;

        /// <summary>Enqueue an object to this fixed size queue, may discard older entries</summary>
        /// <param name="obj">The object to enqueue</param>
        public new void Enqueue(T obj)
        {
            base.Enqueue(obj);

            lock (_fixedQueueLock)
            {
                // Try dequeue until empty.
                while (Count > FixedSize && TryDequeue(out _)) { }
            }
        }
    }
}
