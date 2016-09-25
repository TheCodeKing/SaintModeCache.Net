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
        public void WhenActiveCacheAndRemoveThenKeyIsCaseSensitive()
        {
            // arrange
            var expectedResult1 = "expectedResult1";
            var expectedResult2 = "expectedResult2";
            var cacheKey1 = "key";
            var cacheKey2 = "KEY";
            using (var cache = new SaintModeCache())
            {
                cache.SetOrUpdateWithoutCreate(cacheKey1, expectedResult1);
                cache.SetOrUpdateWithoutCreate(cacheKey2, expectedResult2);
                cache.Remove(cacheKey2);

                // act
                var result1 = cache.GetWithoutCreateOrNull(cacheKey1);
                var result2 = cache.GetWithoutCreateOrNull(cacheKey2);

                // assert
                Assert.That(result1, Is.EqualTo(expectedResult1));
                Assert.That(result2, Is.Null);
            }
        }

        [Test]
        public void WhenActiveCacheItemRemovedThenGetOrCreateTriggersUpdate()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var triggerUpdate = false;
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult, policy);
                cache.Remove(cacheKey);
                var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act
                cache.GetOrCreate(cacheKey, (key, cancel) =>
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
        public void WhenActiveCacheItemThenGetOrCreateReturnsItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenActiveCacheItemThenGetWithoutCreateKeyIsCaseSensitive()
        {
            // arrange
            var expectedResult1 = "expectedResult1";
            var expectedResult2 = "expectedResult2";
            var cacheKey1 = "key";
            var cacheKey2 = "KEY";
            using (var cache = new SaintModeCache())
            {
                cache.SetOrUpdateWithoutCreate(cacheKey1, expectedResult1, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.GetOrCreate(cacheKey2, (k, c) => expectedResult2);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult2));
            }
        }

        [Test]
        public void WhenActiveCacheItemThenGetWithoutCreateOrNullKeyIsCaseSensitive()
        {
            // arrange
            var expectedResult1 = "expectedResult1";
            var cacheKey1 = "key";
            var cacheKey2 = "KEY";
            using (var cache = new SaintModeCache())
            {
                cache.SetOrUpdateWithoutCreate(cacheKey1, expectedResult1, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.GetWithoutCreateOrNull(cacheKey2);

                // assert
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void WhenActiveCacheItemThenGetWithoutCreateOrNullReturnsItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.GetWithoutCreateOrNull(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenActiveCacheItemThenGetWithoutCreateOrNullUpdatesItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult, ObjectCache.InfiniteAbsoluteExpiration);
                cache.SetOrUpdateWithoutCreate(cacheKey, expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.GetWithoutCreateOrNull(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenActiveCacheItemThenIsNotExpired()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);

                // act
                var result = cache.Expired(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenActiveCacheItemThenItemIsNotStale()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenCacheIsEmptyAndCancellationTokenUsedOnUpdateThenDontCacheAndReturnNull()
        {
            // arrange
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) =>
                {
                    cancel.IsCancellationRequested = true;
                    startTests.Set();
                    return unexpectedResult;
                });

                // assert
                startTests.WaitOne();
                Thread.Sleep(20);
                Assert.That(cacheValue, Is.Null);
            }
        }

        [Test]
        public void WhenCustomCacheIsUsedThenCacheItemsAreAddedToCustomCache()
        {
            // arrange
            var expectedResult = "value";
            var cacheKey = "key";
            var memoryCache = new MemoryCache("CustomCache", null);
            using (var cache = new SaintModeCache(memoryCache))
            {
                // act
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(memoryCache[cacheKey], Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenEmptyCacheAndMultipleThreadsUpdateValueThenOnlyOneTheadUpdatesValue()
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
                        return cache.GetOrCreate(cacheKey, (key, cancel) =>
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
        public void WhenEmptyCacheThenGetOrCreateReturnsUpdateItem()
        {
            // arrange
            var expectedResult = "value";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenEmptyCacheThenGetWithoutCreateOrNullReturnsNull()
        {
            // arrange
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                // act
                var result = cache.GetWithoutCreateOrNull(cacheKey);

                // assert
                Assert.That(result, Is.Null);
            }
        }

        [Test]
        public void WhenGetOrCreateWithActiveCacheItemThenNoUpdateToCache()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.SetOrUpdateWithoutCreate(cacheKey, expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult,
                    ObjectCache.InfiniteAbsoluteExpiration);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenInfiniteAbsoluteExpirationCacheItemThenGetOrCreateUpdatesCachePolicy()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult, ObjectCache.InfiniteAbsoluteExpiration);
                cache.SetOrUpdateWithoutCreate(cacheKey, expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenInfiniteAbsoluteExpirationCacheItemThenItemIsNotStale()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.False);
            }
        }

        [Test]
        public void WhenSetOrUpdateWithoutCreateThenGetWithoutCreateOrNullKeyIsCaseSensitive()
        {
            // arrange
            var expectedResult1 = "expectedResult1";
            var expectedResult2 = "expectedResult2";
            var cacheKey1 = "key";
            var cacheKey2 = "KEY";
            using (var cache = new SaintModeCache())
            {
                cache.SetOrUpdateWithoutCreate(cacheKey1, expectedResult1);
                cache.SetOrUpdateWithoutCreate(cacheKey2, expectedResult2);

                // act
                var result1 = cache.GetWithoutCreateOrNull(cacheKey1);
                var result2 = cache.GetWithoutCreateOrNull(cacheKey2);

                // assert
                Assert.That(result1, Is.EqualTo(expectedResult1));
                Assert.That(result2, Is.EqualTo(expectedResult2));
            }
        }

        [Test]
        public void WhenStaleCacheAndMultipleThreadsGetValueThenOnlyOneThreadUpdatesValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";

            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);
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
                        return cache.GetOrCreate(cacheKey, (key, cancel) =>
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
        public void WhenStaleCacheItemExistsThenCacheItemIsExpired()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Expired(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenStaleCacheItemThenGetOrCreateAfterAnUpdateCompletesReturnsUpdatedItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => unexpectedResult, policy);
                Thread.Sleep(20);
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                cache.GetOrCreate(cacheKey, (key, cancel) =>
                {
                    startTests.Set();
                    return expectedResult;
                }, ObjectCache.InfiniteAbsoluteExpiration);
                startTests.WaitOne();
                Thread.Sleep(20);

                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);

                // assert
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenStaleCacheItemThenGetOrCreateReturnsCachedValueAndTriggersUpdate()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var triggerUpdate = false;
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);
                Thread.Sleep(20);
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);

                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) =>
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
        public void WhenStaleCacheItemThenGetWithoutCreateOrNullReturnsCacheItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = ObjectCache.InfiniteAbsoluteExpiration};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);

                // act
                var result = cache.GetWithoutCreateOrNull(cacheKey);

                // assert
                Assert.That(result, Is.EqualTo(expectedResult));
            }
        }

        [Test]
        public void WhenStaleCacheItemThenItemIsStale()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, policy);
                Thread.Sleep(20);

                // act
                var result = cache.Stale(cacheKey);

                // assert
                Assert.That(result, Is.True);
            }
        }

        [Test]
        public void WhenStringInternWithDifferenceCaseStringsThenReferneceIsDifferent()
        {
            // arrange
            var cacheKey1 = "key";
            var cacheKey2 = "KEY";

            // act
            var ref1 = string.Intern(cacheKey1);
            var ref2 = string.Intern(cacheKey2);

            // assert
            Assert.That(ref1, Is.Not.SameAs(ref2));
        }

        [Test]
        public void WhenStringInternWithSameCaseStringsThenReferneceIsSame()
        {
            // arrange
            var cacheKey1 = "key";
            var cacheKey2 = "key";

            // act
            var ref1 = string.Intern(cacheKey1);
            var ref2 = string.Intern(cacheKey2);

            // assert
            Assert.That(ref1, Is.SameAs(ref2));
        }

        [Test]
        public void WheStaleCacheItemExistsAndCancellationTokenUsedThenReturnStaleItem()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            using (var cache = new SaintModeCache())
            {
                var startTests = new EventWaitHandle(false, EventResetMode.ManualReset);
                cache.GetOrCreate(cacheKey, (key, cancel) => expectedResult, DateTime.UtcNow.AddMilliseconds(10));
                Thread.Sleep(20);

                // act
                var cacheValue = cache.GetOrCreate(cacheKey, (key, cancel) =>
                {
                    cancel.IsCancellationRequested = true;
                    startTests.Set();
                    return unexpectedResult;
                });

                // assert
                startTests.WaitOne();
                Thread.Sleep(20);
                Assert.That(cacheValue, Is.EqualTo(expectedResult));
            }
        }
    }
}