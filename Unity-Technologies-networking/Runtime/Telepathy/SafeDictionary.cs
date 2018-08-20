// replaces ConcurrentDictionary which is not available in .NET 3.5 yet.
using System.Collections.Generic;
using System.Linq;

namespace Telepathy
{
    public class SafeDictionary<TKey,TValue>
    {
        Dictionary<TKey,TValue> dict = new Dictionary<TKey,TValue>();

        // for statistics. don't call Count and assume that it's the same after the
        // call.
        public int Count
        {
            get
            {
                lock(dict)
                {
                    return dict.Count;
                }
            }
        }

        public void Add(TKey key, TValue value)
        {
            lock(dict)
            {
                dict[key] = value;
            }
        }

        public void Remove(TKey key)
        {
            lock(dict)
            {
                dict.Remove(key);
            }
        }

        // can't check .ContainsKey before Get because it might change inbetween,
        // so we need a TryGetValue
        public bool TryGetValue(TKey key, out TValue result)
        {
            lock(dict)
            {
                return dict.TryGetValue(key, out result);
            }
        }

        public List<TValue> GetValues()
        {
            lock(dict)
            {
                return dict.Values.ToList();
            }
        }

        public void Clear()
        {
            lock(dict)
            {
                dict.Clear();
            }
        }
    }
}