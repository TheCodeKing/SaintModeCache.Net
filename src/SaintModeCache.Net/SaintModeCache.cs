using System;
using System.Runtime.Caching;
using System.Threading;

namespace SaintModeCaching
{
    public class SaintModeCache
    {
        private const string CacheKeyPrefix = "_#shadow_";
        private readonly MemoryCache storeCache;
        private readonly MemoryCache shadowCache;

        public SaintModeCache()
            : this(new MemoryCache("SaintModeCache", null))
        {
        }

        public SaintModeCache(MemoryCache memoryCache)
        {
            this.storeCache = memoryCache;
            this.shadowCache = new MemoryCache("SaintModeCacheShadow", null);
        }

        public TCacheItem GetOrUpdate<TCacheItem>(string key, Func<string, TCacheItem> fetchDelegate) 
            where TCacheItem : class
        {
            var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
            return GetOrUpdate(key, fetchDelegate, policy);
        }

        public TCacheItem GetOrUpdate<TCacheItem>(string key, Func<string, TCacheItem> fetchDelegate, DateTimeOffset offset)
            where TCacheItem : class
        {
            return GetOrUpdate(key, fetchDelegate, new CacheItemPolicy {AbsoluteExpiration = offset});
        }

        public TCacheItem GetOrUpdate<TCacheItem>(string key, Func<string, TCacheItem> fetchDelegate,
            CacheItemPolicy cachePolicy) where TCacheItem : class
        {
            var shadowKey = GetShadowKey(key);
            var shadowCacheItem = this.shadowCache.Get(shadowKey);
            var item = this.storeCache.Get(key);
            if (shadowCacheItem == null && item != null)
            {
                Action<string, Func<string, object>, CacheItemPolicy> onAsyncUpdateCache = OnAsyncUpdateCache;
                onAsyncUpdateCache.BeginInvoke(key, fetchDelegate, cachePolicy, null, null);
            }

            if (item != null)
            {
                return item as TCacheItem;
            }

            lock (key)
            {
                item = this.storeCache[key];
                if (item != null)
                {
                    return item as TCacheItem;
                }

                item = OnUpdateCache(key, fetchDelegate, cachePolicy);
            }

            return item as TCacheItem;
        }

        private void OnAsyncUpdateCache(string key, Func<string, object> func, CacheItemPolicy cachePolicy)
        {
            var shadowKey = GetShadowKey(key);
            var item = this.shadowCache.Get(shadowKey);
            if (item != null)
            {
                return;
            }

            if (!Monitor.TryEnter(shadowKey))
            {
                return;
            }

            lock (shadowKey)
            {
                item = this.shadowCache.Get(shadowKey);
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
            this.storeCache.Set(key, item, ObjectCache.InfiniteAbsoluteExpiration);
            this.shadowCache.Set(GetShadowKey(key), DateTime.UtcNow, cachePolicy);
            return item;
        }


        private static string GetShadowKey(string key)
        {
            return string.Concat(CacheKeyPrefix, key);
        }
    }
}
