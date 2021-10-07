#nullable enable
using System;
using System.Collections.Generic;
using Verse;

namespace UniversalFermenter
{
    /// <summary>Like Lazy`T`, but faster (non-thread-safe) and can invalidate.</summary>
    public class Cacheable<T>
    {
        private readonly Func<T> valueGetter;
        private bool hasValue;

        private T value = default!;

        public Cacheable(Func<T> valueGetter)
        {
            this.valueGetter = valueGetter;
        }

        public T Value
        {
            get
            {
                if (hasValue)
                    return value;
                value = valueGetter();
                hasValue = true;
                return value;
            }
        }

        public static Cacheable<T> Now(Func<T> valueGetter)
        {
            return new Cacheable<T>(valueGetter) { value = valueGetter() };
        }

        public void Invalidate()
        {
            hasValue = false;
        }

        public static implicit operator T(Cacheable<T> cache)
        {
            if (cache is null) throw new ArgumentNullException(nameof(cache));
            return cache.Value;
        }

        public static implicit operator Cacheable<T>(Func<T> valueGetter)
        {
            return new Cacheable<T>(valueGetter);
        }

        public override string ToString()
        {
            return Value.ToStringSafe();
        }
    }

    public class CacheableDict<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> inner = new Dictionary<TKey, TValue>();
        private readonly Func<TKey, TValue> valueGetter;

        public CacheableDict(Func<TKey, TValue> valueGetter)
        {
            this.valueGetter = valueGetter;
        }

        public TValue Get(TKey key)
        {
            if (inner.TryGetValue(key, out TValue value))
                return value;

            value = valueGetter(key);
            inner.Add(key, value);
            return value;
        }

        public void Invalidate()
        {
            inner.Clear();
        }

        public void Invalidate(TKey key)
        {
            inner.Remove(key);
        }
    }
}
