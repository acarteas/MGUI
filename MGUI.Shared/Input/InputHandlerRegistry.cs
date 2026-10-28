using System;
using System.Collections;
using System.Collections.Generic;

namespace MGUI.Shared.Input
{

    /// <summary>Owns automatic handlers, and observes caller-owned manual handlers without retaining their owners.</summary>
    internal sealed class InputHandlerRegistry<T> : IReadOnlyList<T> where T : class
    {
        private sealed class Entry
        {
            private readonly T StrongHandler;
            private readonly WeakReference<T> WeakHandler;

            internal Entry(T Handler, bool IsManualUpdate)
            {
                if (IsManualUpdate)
                {
                    WeakHandler = new(Handler);
                }
                else
                {
                    StrongHandler = Handler;
                }
            }

            internal bool TryGetHandler(out T Handler)
            {
                Handler = StrongHandler;
                return Handler != null || WeakHandler.TryGetTarget(out Handler);
            }
        }

        private readonly List<Entry> Entries = new();

        internal void Add(T Handler, bool IsManualUpdate)
        {
            Prune();
            Entries.Add(new(Handler, IsManualUpdate));
        }

        internal void Remove(T Handler)
            => Entries.RemoveAll(Entry => !Entry.TryGetHandler(out T Current) || ReferenceEquals(Current, Handler));

        private void Prune() => Entries.RemoveAll(Entry => !Entry.TryGetHandler(out _));

        private List<T> Snapshot()
        {
            List<T> Result = new();
            foreach (Entry Entry in Entries)
            {
                if (Entry.TryGetHandler(out T Handler))
                {
                    Result.Add(Handler);
                }
            }
            Prune();
            return Result;
        }

        public int Count
        {
            get
            {
                Prune();
                return Entries.Count;
            }
        }
        public T this[int Index] => Snapshot()[Index];
        public IEnumerator<T> GetEnumerator() => Snapshot().GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
