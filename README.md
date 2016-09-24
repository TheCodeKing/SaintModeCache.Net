# SaintModeCache.Net
## Overview
SaintModeCache is a threadSafe in-memory cache wrapper for performance optimisations. It's able to continue serving stale content after expiry, whilst it repopulates the cache with a non-blocking guaranteed single thread.

It's ideal for websites which have slow integration points or expensive data processes which can affect the user experience. The SaintMode cache allows developers to optimise caching around any integration points, and ensures the users aren't affected by slow underperforming dependencies such as databases, or web services.

Note the cache does not fetch new data until a cache key has been requested and after it has expired. At this point it will trigger a refresh of the cache item on a background thread, and continue serving stale content until the process completes.

To cater for cache misses, like on application startup, the cache will block all threads requesting a missing cache key, and allow only a single thread to populate the cache. Once the value is populated, all threads requesting the same cache key can access item. This prevents overloading external systems when the cache is cleared.

## Quick Start
### Install
``` 
Install-Package SaintModeCache.Net
```
### SaintMode Caching
Use the GetOrCreate Method to leveage SaintMode caching mode. This requires a delegate which will be used to creaate the value in the case of a cache miss. If a cache item already exists then it's returned without attempting to create. If the cache item exists but has expired, then the stale item is returned the the caller whilse the delegate is used to refresh the cache on a backgorund thread.
```
var cache = new SaintModeCache();
var cacheKey = "customer123";
var cacheTimeInSeconds = 60;
var cachedValue = cache.GetOrCreate(cacheKey, k => {
        // access remote resources and get cachable data
        return new DataModel();
    },
    cacheTimeInSeconds);
```
## Usage
### Constructors
Create with defaults which has an infinite expires policy when not overriden.
``` CSharpe
var cache = new SaintModeCache();
```
Create with given expires policy in seconds. Used when not overriden.
``` CSharpe
var cache = new SaintModeCache(60);
```
Create with custom ObjectCache for storing values.
``` CSharpe
var cache = new SaintModeCache(new MemoryCache("MyCache", null));
```
Create with custom ObjectCache for storing values and a default expires policy in seconds.
``` CSharpe
var cache = new SaintModeCache(new MemoryCache("MyCache", null), 60);
```
### GetOrCreate
Get cache value is exists or block for single thread to create cache item. Uses the default expires policy from constructor if set, or inifinite.
``` CSharpe
cache.GetOrCreate("MyKey", k =>
        string.Concat("Value for key: ", k));
```
As above but overrides default expires policy with a custom timeout in seconds.       
``` CSharpe
var expiresPolicy = DateTime.UtcNow.AddSeconds(20);
cache.GetOrCreate("MyKey", k =>
        string.Concat("Value for key: ", k),
        expiresPolicy);
```        
As above but uses CacheItemPolicy instance. 
``` CSharpe
var expiresPolicy = new CacheItemPolicy { 
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)};
cache.GetOrCreate("MyKey", k =>
        string.Concat("Value for key: ", k),
        expiresPolicy);
```
### Stale
A cache item is stale when it has expired, but has not yet progressed a new value to replace it. Once stale, a background fetch is only triggered by a call to GetOrCreate.
``` CSharpe
var isStale = Stale("MyKey", new object());
```
### Expired
A cache item has expired when the cache policy defined when it was added has passed. However the item may still be in cache in a stale state.
``` CSharpe
var hasExpired = Expired("MyKey");
```
### TryGet
Try to get an existing item from the cache if it exists. Saint mode is not available via this interface, therefore it will return false if no item exists.
``` CSharpe
if (cache.TryGet("MyKey", out result)) {
    // do something with result
}
```
As above, but includes a value to include whether the data is stale.
``` CSharpe
if (cache.TryGet("MyKey", out result, out stale)) {
    // do something with result
}
```
### SetOrUpdateWithoutCreate
Set or update an item in the cache directly. This will update an existing item and change it's expires policy to the default which is inifinite.
``` CSharpe
SetOrUpdateWithoutCreate("MyKey", new object());
```
Set or update an item in the cache directly and define a new expires policy. This will update an existing item and change it's expires policy to a timeout in seconds.
``` CSharpe
SetOrUpdateWithoutCreate("MyKey", new object(), 60);
```
Set or update an item in the cache directly and define a new expires policy. This will update an existing item and change the policy to the one provided.
``` CSharpe
var expiresPolicy = new CacheItemPolicy { 
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)};
SetOrUpdateWithoutCreate("MyKey", new object(), expiresPolicy);
```
Set or update an item in the cache directly using a CacheItem instance. The default expires policy of inifinite will be used.
``` CSharpe
var expiresPolicy = new CacheItemPolicy { 
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)};
SetOrUpdateWithoutCreate("MyKey", new object(), expiresPolicy);
```
Set or update an item in the cache directly using a CacheItem instance. The default expires policy of inifinite will be used.
``` CSharpe
var expiresPolicy = new CacheItemPolicy { 
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)};
SetOrUpdateWithoutCreate("MyKey", new object(), expiresPolicy);
```
### GetWithoutCreateOrNull
Get an existing item from the cache if it exists or return null. SaintMode is not available via this interface, but it can be used to access existing values where they exist.
``` CSharpe
var cacheValue = GetWithoutCreateOrNull("MyKey");
```
### Remove
Explicitly remove an item from the cache as follows and return the last value.
``` CSharpe
var removedValue = Remove("MyKey");
```













