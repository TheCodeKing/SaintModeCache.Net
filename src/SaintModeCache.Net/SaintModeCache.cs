using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading;
using Conditions;

namespace SaintModeCaching
{
    public sealed class SaintModeCache : ISaintModeCache
    {
        private const string ShadowKeyPrefixacheName = "__Shadow#";
        private const string CacheName = "__SaintModeCache";
        private const string ShadowCacheName = "__SaintModeCacheShadow";
        private readonly bool disposeStore;
        private readonly ObjectCache shadowCache;
        private readonly ObjectCache storeCache;
        private uint? defaultTimeoutSeconds;
        private bool disposed;

        public SaintModeCache(uint defaultTimeoutSeconds)
            : this(new MemoryCache(CacheName))
        {
            disposeStore = true;
            this.defaultTimeoutSeconds = defaultTimeoutSeconds;
        }

        public SaintModeCache()
            : this(new MemoryCache(CacheName))
        {
            disposeStore = true;
        }

        public SaintModeCache(ObjectCache customCache, uint defaultTimeoutSeconds)
            : this(customCache)
        {
            this.defaultTimeoutSeconds = defaultTimeoutSeconds;
        }

        public SaintModeCache(ObjectCache customCache)
        {
            customCache.Requires("customCache").IsNotNull();

            storeCache = customCache;
            shadowCache = new MemoryCache(ShadowCacheName, null);
        }

        public TCacheItem AddOrGetExisting<TCacheItem>(string key, Func<string, TCacheItem> updateCache)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();

            return AddOrGetExisting(key, updateCache, GetDefaultOffset());
        }

        public TCacheItem AddOrGetExisting<TCacheItem>(string key, Func<string, TCacheItem> updateCache,
            DateTimeOffset absoluteExpiration)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();

            return AddOrGetExisting(key, updateCache, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        public TCacheItem AddOrGetExisting<TCacheItem>(string key, Func<string, TCacheItem> updateCache,
            CacheItemPolicy cachePolicy) where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();
            cachePolicy.Requires("cachePolicy").IsNotNull();

            var item = Get(key);
            if (Expired(key) && item != null)
            {
                Action<string, Func<string, object>, CacheItemPolicy> onAsyncUpdateCache = OnAsyncUpdateCache;
                onAsyncUpdateCache.BeginInvoke(key, updateCache, cachePolicy, null, null);
            }

            if (item != null)
            {
                return item as TCacheItem;
            }

            lock (key)
            {
                item = Get(key);
                if (item != null)
                {
                    return item as TCacheItem;
                }

                item = OnUpdateCache(key, updateCache, cachePolicy);
            }

            return item as TCacheItem;
        }

        public bool Contains(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return storeCache.Contains(key);
        }

        public CacheEntryChangeMonitor CreateCacheEntryChangeMonitor(IEnumerable<string> keys)
        {
            return storeCache.CreateCacheEntryChangeMonitor(keys);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool Expired(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            var shadowKey = GetShadowKey(key);
            var shadowCacheItem = shadowCache.Get(shadowKey);
            return shadowCacheItem == null;
        }

        public object Get<TCacheItem>(string key)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return Get(key) as TCacheItem;
        }

        public object Get(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return storeCache.Get(key);
        }

        public object Remove(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            var shadowKey = GetShadowKey(key);
            object item;

            lock (key)
            {
                item = storeCache.Remove(key);
                shadowCache.Remove(shadowKey);
            }

            return item;
        }

        public void Set(string key, object value)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            Set(key, value, GetDefaultOffset());
        }

        public void Set(string key, object value, DateTimeOffset absoluteExpiration)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            Set(key, value, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        public void Set(string key, object value, CacheItemPolicy policy)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            policy.Requires("policy").IsNotNull();

            Set(new CacheItem(key, value), policy);
        }

        public void Set(CacheItem item)
        {
            item.Requires("item").IsNotNull();

            Set(item, GetDefaultOffset());
        }

        public void Set(CacheItem item, CacheItemPolicy policy)
        {
            item.Requires("item").IsNotNull();
            policy.Requires("policy").IsNotNull();

            lock (item.Key)
            {
                storeCache.Set(item.Key, item.Value, ObjectCache.InfiniteAbsoluteExpiration);
                shadowCache.Set(GetShadowKey(item.Key), DateTime.UtcNow, policy);
            }
        }

        public bool Stale(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            var item = storeCache.Get(key);
            return Expired(key) && item != null;
        }

        public DefaultCacheCapabilities DefaultCacheCapabilities => storeCache.DefaultCacheCapabilities;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) storeCache).GetEnumerator();
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            IEnumerable<KeyValuePair<string, object>> enumerable = storeCache;
            return enumerable.GetEnumerator();
        }

        private static string GetShadowKey(string key)
        {
            return string.Concat(ShadowKeyPrefixacheName, key);
        }

        private static bool IsLocked(string shadowKey)
        {
            if (Monitor.TryEnter(shadowKey))
            {
                Monitor.Exit(shadowKey);
            }
            else
            {
                return true;
            }
            return false;
        }

        public void Set(CacheItem item, DateTimeOffset absoluteExpiration)
        {
            item.Requires("item").IsNotNull();

            Set(item, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            if (disposing)
            {
                if (disposeStore)
                {
                    var cache = storeCache as IDisposable;
                    cache?.Dispose();
                }

                var shadow = shadowCache as IDisposable;
                shadow?.Dispose();
            }

            disposed = true;
        }

        private DateTimeOffset GetDefaultOffset()
        {
            return defaultTimeoutSeconds.HasValue
                ? DateTime.UtcNow.AddSeconds(defaultTimeoutSeconds.Value)
                : ObjectCache.InfiniteAbsoluteExpiration;
        }

        private void OnAsyncUpdateCache(string key, Func<string, object> func, CacheItemPolicy cachePolicy)
        {
            var shadowKey = GetShadowKey(key);
            var item = shadowCache.Get(shadowKey);
            if (item != null)
            {
                return;
            }

            if (IsLocked(shadowKey))
            {
                return;
            }

            lock (shadowKey)
            {
                item = shadowCache.Get(shadowKey);
                if (item != null)
                {
                    return;
                }

                OnUpdateCache(key, func, cachePolicy);
            }
        }

        private object OnUpdateCache(string key, Func<string, object> func, CacheItemPolicy cachePolicy)
        {
            var item = func(key);
            Set(key, item, cachePolicy);
            return item;
        }

        ~SaintModeCache()
        {
            Dispose(false);
        }
    }
}