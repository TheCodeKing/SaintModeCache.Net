using System;
using System.Runtime.Caching;
using System.Threading;
using NUnit.Framework;

namespace SaintModeCaching.Tests
{
    [TestFixture]
    public class SaintModeCacheTests
    {
        [Test]
        public void WhenAddOrGetExistingWithExistingCacheThenNoUpdateToCache()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.Set(cacheKey, expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.AddOrGetExisting(cacheKey, key => unexpectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenCacheExpiresMultipleThreadsRequestValueThenOnlyOneCreatesValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";

            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);
                Thread.Sleep(200);
                var counter = 0;
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                var waitForTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                var waitForResult = new EventWaitHandle(false, EventResetMode.ManualReset);
                var triggerUpdate = false;

                // act //assert
                for (var i = 0; i < 1000; i++)
                {
                    Func<object> testCache = () =>
                    {
                        startTests.WaitOne();
                        return cache.AddOrGetExisting(cacheKey, key =>
                        {
                            triggerUpdate = true;
                            Interlocked.Increment(ref counter);
                            waitForResult.Set();
                            return expectedResult;
                        }, policy);
                    };

                    testCache.BeginInvoke(re =>
                    {
                        var result = testCache.EndInvoke(re);
                        try
                        {
                            Assert.That(result, Is.EqualTo(expectedResult));
                            Assert.That(counter, Is.LessThanOrEqualTo(1));
                        }
                        catch
                        {
                            waitForTests.Set();
                            waitForResult.Set();
                            throw;
                        }

                        if ((int) re.AsyncState != 999)
                        {
                            return;
                        }

                        waitForTests.Set();
                    }, i);
                }

                startTests.Set();
                waitForTests.WaitOne();
                waitForResult.WaitOne();

                Assert.That(triggerUpdate, Is.True);
            }
        }

        [Test]
        public void WhenCacheObjectNotStaleThenShowNotStaleKey()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenCustomCacheUsedThenAddToCustomCache()
        {
            // arrange
            var expectedResult = "value";
            var cacheKey = "key";
            var memoryCache = new MemoryCache("CustomCache", null);
            using (var cache = new SaintModeCache(memoryCache))
            {
                // act
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(memoryCache[cacheKey], Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenExpiredCacheObjectThenGetReturnsObject()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);

                // act
                var result = cache.Get(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenExpiredCacheObjectThenShowExpiredKey()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Expired(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenInifiniteCacheObjectThenNeverStale()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenNoCacheMultipleThreadsRequestValueThenOnlyOneCreatesValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var counter = 0;
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                var waitForTests = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act //assert
                for (var i = 0; i < 1000; i++)
                {
                    Func<object> testCache = () =>
                    {
                        startTests.WaitOne();
                        return cache.AddOrGetExisting(cacheKey, key =>
                        {
                            Interlocked.Increment(ref counter);
                            return expectedResult;
                        });
                    };

                    testCache.BeginInvoke(re =>
                    {
                        var result = testCache.EndInvoke(re);
                        try
                        {
                            Assert.That(result, Is.EqualTo(expectedResult));
                            Assert.That(counter, Is.LessThanOrEqualTo(1));
                        }
                        catch
                        {
                            waitForTests.Set();
                            throw;
                        }

                        if ((int) re.AsyncState == 999)
                        {
                            waitForTests.Set();
                        }
                    }, i);
                }

                startTests.Set();
                waitForTests.WaitOne();
            }
        }

        [Test]
        public void WhenNoCacheObjectThenContainsReturnsFalse()
        {
            // arrange
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                // act
                var result = cache.Contains(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenNoCacheObjectThenGetReturnsNull()
        {
            // arrange
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                // act
                var result = cache.Get(cacheKey);

                // assert
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void WhenNotExpiredCacheObjectThenGetReturnsObject()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Get(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenNotExpiredCacheObjectThenShowNotExpiredKey()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);

                // act
                var result = cache.Expired(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenNotStaleCacheObjectThenContainsReturnsTrue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Contains(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenObjectExpiredThenReturnOldResultAndTriggerRefresh()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var triggerUpdate = false;
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);
                Thread.Sleep(20);
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act
                var cacheValue = cache.AddOrGetExisting(cacheKey, key =>
                {
                    triggerUpdate = true;
                    startTests.Set();
                    return unexpectedResult;
                }, policy);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
                startTests.WaitOne();
                Assert.That(triggerUpdate, Is.True);
            }
        }

        [Test]
        public void WhenObjectExpiresThenAfterRefreshReturnUpdatedValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => unexpectedResult, policy);
                Thread.Sleep(20);
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                cache.AddOrGetExisting(cacheKey, key =>
                {
                    startTests.Set();
                    return expectedResult;
                }, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                startTests.WaitOne();
                var cacheValue = cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenObjectInCacheThenReturnResult()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var cacheValue = cache.AddOrGetExisting(cacheKey, key => unexpectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);
                cache.AddOrGetExisting(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenObjectNotInCacheThenReturnResult()
        {
            // arrange
            var expectedResult = "value";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                // act
                var cacheValue = cache.AddOrGetExisting(cacheKey, key => expectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenRemoveCacheObjectThenAddOrGetExistingTriggersFetch()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var triggerUpdate = false;
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.AddOrGetExisting(cacheKey, key => unexpectedResult, policy);
                cache.Remove(cacheKey);
                var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act
                cache.AddOrGetExisting(cacheKey, key =>
                {
                    triggerUpdate = true;
                    ewh.Set();
                    return expectedResult;
                }, policy);

                // assert
                ewh.WaitOne(5000);
                Assert.That(triggerUpdate, Is.True);
            }
        }

        [Test]
        public void WhenSetExistingCacheObjectThenChangeTimeToLiveKey()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => unexpectedResult, ObjectCache.InfiniteAbsoluteExpiration);
                cache.Set(cacheKey, expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenSetExistingCacheObjectThenValueIsChangedImmediately()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => unexpectedResult, ObjectCache.InfiniteAbsoluteExpiration);
                cache.Set(cacheKey, expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Get(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenStaleCacheObjectThenContainsReturnsTrue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Contains(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenStaleCacheObjectThenShowStaleKey()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.AddOrGetExisting(cacheKey, key => expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }
    }
}