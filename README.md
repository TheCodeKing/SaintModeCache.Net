# SaintModeCache.Net 
## Overview 
SaintModeCache is a thread safe in-memory cache wrapper for performance optimisation. It's able to continue serving stale content after expiry, whilst it repopulates the cache with a non-blocking single thread. 
 
It's ideal for websites which have slow integration points or expensive data processes which can affect the user experience. The SaintMode cache allows developers to optimise caching around any integration points, and ensures that users aren't affected by slow under performing dependencies such as databases, or web services. 
 
Note the cache does not fetch new data until a cache key has been requested and after it has expired. At this point it will trigger a refresh of the cache item on a background thread, and continue serving stale content until the process completes. 
 
To cater for cache misses, like on application startup, the cache will block all threads requesting a missing cache key, and allow only a single thread to populate the cache. Once the value is populated, all threads requesting the same cache key can access item. This prevents overloading external systems when the cache is cleared.

## Live Demo
The following live demo uses the SaintModeCache server-side to minimise calls to various back-end web services it consumes. It also removes any wait time for the users whilst loading data to refresh the feeds. Every request is rendered server-side in real-time.

http://saintmodedemo.azurewebsites.net
 
## Quick Start 
### Install 
```  
Install-Package SaintModeCache.Net 
``` 
### SaintMode Caching 
Use the GetOrCreate Method to leverage SaintMode caching mode. This requires a delegate which will be used to create the value in the case of a cache miss. If a cache item already exists then it's returned without attempting to create. If the cache item exists but has expired, then the stale item is returned the the caller whilst the delegate is used to refresh the cache on a background thread. Note cache keys are case sensitive, thus "key" returns a different cache item to "KEY".
``` 
var cacheKey = "customer123";
var cacheTimeInSeconds = 60;
var customerModel = Cache.GetOrCreate(cacheKey,
        (key, cancelToken) => slowUnreliableService.GetCustomerModel(key),
        cacheTimeInSeconds);
``` 
### UpdateCacheCancellationToken
The update delegate is passed an instance of UpdateCacheCancellationToken. This can be used to cancel the update of the cache if needed, and force the system to contuniue serving stale cache items. In this case the expires policy is not reset, and the next attempt to read from cache will trigger another attempt for update. 

Warning if a cache item is removed or evicted from cache for any reason, and the update request is cancelled, then the cache will return a null value. The recommended approach to avoid null values is to always return a value and avoid cancelling updates.
``` 
var cache = new SaintModeCache(); 
var cacheKey = "customer123"; 
var cacheTimeInSeconds = 60; 

// initalise a fallback cache item in case of cancel
cache.SetOrUpdateWithoutCreate(cacheKey, DataModel.Default, cacheTimeInSeconds);

// continues to return the previously set cache item, even after it expirie
var cachedValue = cache.GetOrCreate(cacheKey, (key,cancelToken) => { 
        // on failure to get data from remote resources
        cancelToken.IsCancellationRequested = true;
        return null; 
    }, 
    cacheTimeInSeconds); 
``` 
## Usage 
### Constructors 
Create with defaults which has an infinite expires policy when not overridden. 
``` CSharpe 
var cache = new SaintModeCache(); 
``` 
Create with given expires policy in seconds. Used when not overridden. 
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
Get cache value if exists or block for single thread to create cache item. Uses the default expires policy from constructor if set, or default policy defined by constructor.
``` CSharpe 
cache.GetOrCreate("MyKey", (key, cancelToken) => 
        string.Concat("Value for key: ", key)); 
``` 
As above but overrides default expires policy with a custom timeout in seconds.        
``` CSharpe 
var expiresPolicy = DateTime.UtcNow.AddSeconds(20); 
cache.GetOrCreate("MyKey", (key, cancelToken) => 
        string.Concat("Value for key: ", key), 
        expiresPolicy); 
```         
As above but uses CacheItemPolicy instance.  
``` CSharpe 
var expiresPolicy = new CacheItemPolicy {  
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20) }; 
cache.GetOrCreate("MyKey", (key, cancelToken) => 
        string.Concat("Value for key: ", key), 
        expiresPolicy); 
``` 
### Stale 
A cache item is stale when it has expired, but has not yet fetched an updated value to replace it. Once stale, a background fetch is only triggered by a call to GetOrCreate. 
``` CSharpe 
var isStale = Stale("MyKey", new object()); 
``` 
### Expired 
A cache item has expired when it's cache policy has passed. However the item may still be in cache in a stale state. 
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
Set or update an item in the cache directly. This will update an existing item and change it's expires policy to the default defined by the constructor. 
``` CSharpe 
SetOrUpdateWithoutCreate("MyKey", new object()); 
``` 
Set or update an item in the cache directly and define a new expires policy. This will update an existing item and change it's expires policy to a timeout in seconds. 
``` CSharpe 
var expiresPolicy = DateTime.UtcNow.AddSeconds(60); 
SetOrUpdateWithoutCreate("MyKey", new object(), expiresPolicy); 
``` 
Set or update an item in the cache directly and define a new expires policy. This will update an existing item and change the cache policy to the one provided. 
``` CSharpe 
var expiresPolicy = new CacheItemPolicy {  
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)}; 
SetOrUpdateWithoutCreate("MyKey", new object(), expiresPolicy); 
``` 
Set or update an item in the cache directly using a CacheItem instance. The default expires policy defined by the constructor is used. 
``` CSharpe 
var cacheItem = new CacheItem("MyKey", new object());
SetOrUpdateWithoutCreate(cacheItem); 
``` 
Set or update an item in the cache directly using a CacheItem instance. This will update an existing item and change it's expires policy to a timeout in seconds.
``` CSharpe 
var cacheItem = new CacheItem("MyKey", new object());
var expiresPolicy = new CacheItemPolicy {  
    AbsoluteExpiration = DateTime.UtcNow.AddSeconds(20)}; 
SetOrUpdateWithoutCreate(cacheItem, expiresPolicy); 
``` 
### GetWithoutCreateOrNull 
Get an existing item from the cache if it exists or return null. SaintMode is not available via this interface, but it can be used to access existing values where they exist.
``` CSharpe 
var cacheValue = GetWithoutCreateOrNull("MyKey"); 
``` 
### Remove 
Explicitly remove an item from the cache and return the last value. 
``` CSharpe 
var removedValue = Remove("MyKey"); 
``` 
