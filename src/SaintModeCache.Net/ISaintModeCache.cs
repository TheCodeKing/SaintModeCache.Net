using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace SaintModeCaching
{
    public interface ISaintModeCache : IEnumerable<KeyValuePair<string, object>>, IDisposable, IEnumerable
    {
        DefaultCacheCapabilities DefaultCacheCapabilities { get; }

        bool Contains(string key);

        CacheEntryChangeMonitor CreateCacheEntryChangeMonitor(IEnumerable<string> keys);

        bool Expired(string key);

        TCacheItem GetOrCreate<TCacheItem>(string key, Func<string, TCacheItem> updateCache)
            where TCacheItem : class;

        TCacheItem GetOrCreate<TCacheItem>(string key, Func<string, TCacheItem> updateCache, DateTimeOffset offset)
            where TCacheItem : class;

        TCacheItem GetOrCreate<TCacheItem>(string key, Func<string, TCacheItem> updateCache,
            CacheItemPolicy cachePolicy) where TCacheItem : class;

        TCacheItem GetWithoutCreateOrNull<TCacheItem>(string key)
            where TCacheItem : class;

        object GetWithoutCreateOrNull(string key);

        DateTime? LastUpdatedDateTimeUtc(string key);

        object Remove(string key);

        void SetOrUpdateWithoutCreate(string key, object value);

        void SetOrUpdateWithoutCreate(string key, object value, DateTimeOffset absoluteExpiration);

        void SetOrUpdateWithoutCreate(string key, object value, CacheItemPolicy policy);

        void SetOrUpdateWithoutCreate(CacheItem item, CacheItemPolicy policy);

        void SetOrUpdateWithoutCreate(CacheItem item);

        bool Stale(string key);
    }
}