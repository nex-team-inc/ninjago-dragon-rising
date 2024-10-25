#nullable enable

using System.Collections;
using System.Collections.Generic;

namespace Nex.Utils
{
    public struct HistoryItem<T>
    {
        public T Item;
        public float Timestamp;
    }

    public class History<T> : IEnumerable<HistoryItem<T>>
    {
        readonly float maxAge;
        readonly Queue<HistoryItem<T>> history = new();

        public HistoryItem<T>? Last { get; private set; }
        public HistoryItem<T>? Peek => history.Count > 0 ? history.Peek() : null;

        public History(float maxAge)
        {
            this.maxAge = maxAge;
        }

        public void Add(float timestamp, T data)
        {
            if (timestamp <= Last?.Timestamp) return;
            var item = new HistoryItem<T> { Timestamp = timestamp, Item = data };
            Last = item;
            history.Enqueue(item);
            CleanUp();
        }

        public void Clear()
        {
            history.Clear();
            Last = default;
        }

        void CleanUp()
        {
            while (history.Count > 0 && history.Peek().Timestamp <= Last?.Timestamp - maxAge)
            {
                history.Dequeue();
            }

            if (history.Count == 0) Last = default;
        }

        public IEnumerator<HistoryItem<T>> GetEnumerator() => history.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => history.GetEnumerator();
    }
}
