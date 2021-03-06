﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading;
using Conditions;

namespace SaintModeCaching
{
    public sealed class SaintModeCache : ISaintModeCache
    {
        private const string CacheName = "__SaintModeCache";
        private const string LockNamePrefix = "__LockShadow#";
        private const string ShadowCacheName = "__SaintModeCacheShadow";
        private const string ShadowKeyPrefixCacheName = "__Shadow#";
        private static readonly object DummyShadowObject = string.Intern(string.Empty);
        private readonly bool disposeStore;

        private readonly Action<string, Func<string, CacheUpdateCancellationToken, object>, CacheItemPolicy>
            onAsyncUpdateCache;

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
            onAsyncUpdateCache = OnAsyncUpdateCache;
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
            return !shadowCache.Contains(shadowKey);
        }

        public TCacheItem GetOrCreate<TCacheItem>(string key,
            Func<string, CacheUpdateCancellationToken, TCacheItem> updateCache)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();

            return GetOrCreate(key, updateCache, GetDefaultOffset());
        }

        public TCacheItem GetOrCreate<TCacheItem>(string key,
            Func<string, CacheUpdateCancellationToken, TCacheItem> updateCache,
            DateTimeOffset absoluteExpiration)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();

            return GetOrCreate(key, updateCache, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        public TCacheItem GetOrCreate<TCacheItem>(string key,
            Func<string, CacheUpdateCancellationToken, TCacheItem> updateCache,
            CacheItemPolicy cachePolicy) where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            updateCache.Requires("updateCache").IsNotNull();
            cachePolicy.Requires("cachePolicy").IsNotNull();

            object item;
            bool stale;
            if (TryGet(key, out item, out stale))
            {
                if (stale)
                {
                    onAsyncUpdateCache.BeginInvoke(key, updateCache, cachePolicy, null, null);
                }

                return item as TCacheItem;
            }

            var shadowKey = GetShadowKey(key);
            var shadowLock = GetLock(shadowKey);
            lock (shadowLock)
            {
                if (TryGet(key, out item))
                {
                    return item as TCacheItem;
                }

               item = OnUpdateCache(key, updateCache, cachePolicy);
                return item as TCacheItem;
            }
        }

        public TCacheItem GetWithoutCreateOrNull<TCacheItem>(string key)
            where TCacheItem : class
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return GetWithoutCreateOrNull(key) as TCacheItem;
        }

        public object GetWithoutCreateOrNull(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return storeCache.Get(key);
        }

        public object Remove(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            var shadowKey = GetShadowKey(key);
            object item;

            lock (GetLock(key))
            {
                item = storeCache.Remove(key);
                shadowCache.Remove(shadowKey);
            }

            return item;
        }

        public void SetOrUpdateWithoutCreate(string key, object value)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            SetOrUpdateWithoutCreate(key, value, GetDefaultOffset());
        }

        public void SetOrUpdateWithoutCreate(string key, object value, DateTimeOffset absoluteExpiration)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            SetOrUpdateWithoutCreate(key, value, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        public void SetOrUpdateWithoutCreate(string key, object value, CacheItemPolicy policy)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();
            policy.Requires("policy").IsNotNull();

            SetOrUpdateWithoutCreate(new CacheItem(key, value), policy);
        }

        public void SetOrUpdateWithoutCreate(CacheItem item)
        {
            item.Requires("item").IsNotNull();

            Set(item, GetDefaultOffset());
        }

        public void SetOrUpdateWithoutCreate(CacheItem item, CacheItemPolicy policy)
        {
            item.Requires("item").IsNotNull();
            policy.Requires("policy").IsNotNull();
            var shadowKey = GetShadowKey(item.Key);

            lock (GetLock(item.Key))
            {
                storeCache.Set(item.Key, item.Value, ObjectCache.InfiniteAbsoluteExpiration);
                shadowCache.Set(shadowKey, DummyShadowObject, policy);
            }
        }

        public bool Stale(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return Expired(key) && Contains(key);
        }

        public bool TryGet(string key, out object item)
        {
            bool isStale;
            return TryGet(key, out item, out isStale);
        }

        public bool TryGet(string key, out object item, out bool stale)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            lock (GetLock(key))
            {
                var contains = Contains(key);
                stale = contains && Expired(key);
                item = storeCache.Get(key);
                return contains;
            }
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

        private static object GetLock(string key)
        {
            return string.Intern(string.Concat(LockNamePrefix, key));
        }

        private static string GetShadowKey(string key)
        {
            return string.Concat(ShadowKeyPrefixCacheName, key);
        }

        public void Set(CacheItem item, DateTimeOffset absoluteExpiration)
        {
            item.Requires("item").IsNotNull();

            SetOrUpdateWithoutCreate(item, new CacheItemPolicy {AbsoluteExpiration = absoluteExpiration});
        }

        private bool Contains(string key)
        {
            key.Requires("key").IsNotNullOrWhiteSpace();

            return storeCache.Contains(key);
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

        private void OnAsyncUpdateCache(string key, Func<string, CacheUpdateCancellationToken, object> func,
            CacheItemPolicy cachePolicy)
        {
            var shadowKey = GetShadowKey(key);
            var shadowLock = GetLock(shadowKey);
            if (!Expired(key))
            {
                return;
            }

            if (Monitor.TryEnter(shadowLock))
            {
                try
                {
                    if (!Expired(key))
                    {
                        return;
                    }

                    OnUpdateCache(key, func, cachePolicy);
                }
                finally
                {
                    Monitor.Exit(shadowLock);
                }
            }
        }

        private object OnUpdateCache(string key, Func<string, CacheUpdateCancellationToken, object> func,
            CacheItemPolicy cachePolicy)
        {
            var updateCancellationToken = new CacheUpdateCancellationToken(cachePolicy);
            var item = func(key, updateCancellationToken);
            if (updateCancellationToken.IsCancellationRequested)
            {
                return null;
            }

            var policy = updateCancellationToken.CacheItemPolicy ?? cachePolicy;
            SetOrUpdateWithoutCreate(key, item, policy);
            return item;
        }

        ~SaintModeCache()
        {
            Dispose(false);
        }

        public sealed class CacheUpdateCancellationToken
        {
            public CacheItemPolicy CacheItemPolicy { get; set; }

            internal CacheUpdateCancellationToken(CacheItemPolicy cacheItemPolicy)
            {
                cacheItemPolicy.Requires("cacheItemPolicy").IsNotNull();

                this.CacheItemPolicy = cacheItemPolicy;
            }

            public bool IsCancellationRequested { get; set; } = false;
        }
    }
}