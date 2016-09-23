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
        public void WhenObjectNotInCacheThenReturnResult()
        {
            // arrange
            var expectedResult = "value";
            var cacheKey = "key";
            var cache = new SaintModeCache();

            // act
            var cacheValue = cache.GetOrUpdate(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

            // assert
            Assert.That(cacheValue, Is.EqualTo(expectedResult));
        }

        [Test]
        public void WhenObjectInCacheThenReturnResult()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var cache = new SaintModeCache();
            cache.GetOrUpdate(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

            // act
            var cacheValue = cache.GetOrUpdate(cacheKey, key => unexpectedResult, ObjectCache.InfiniteAbsoluteExpiration);
            cache.GetOrUpdate(cacheKey, key => expectedResult, ObjectCache.InfiniteAbsoluteExpiration);

            // assert
            Assert.That(cacheValue, Is.EqualTo(expectedResult));
        }

        [Test]
        public void WhenObjectExpiredThenReturnOldResultAndTriggerRefresh()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var triggerUpdate = false;
            var cache = new SaintModeCache();
            var policy = new CacheItemPolicy {AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10)};
            cache.GetOrUpdate(cacheKey, key => expectedResult, policy);
            Thread.Sleep(20);
            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);

            // act
            var cacheValue = cache.GetOrUpdate(cacheKey, key =>
            {
                triggerUpdate = true;
                ewh.Set();
                return unexpectedResult;
            }, policy);

            // assert
            Assert.That(cacheValue, Is.EqualTo(expectedResult));
            ewh.WaitOne();
            Assert.That(triggerUpdate, Is.True);

        }

        [Test]
        public void WhenObjectExpiresThenAfterRefreshReturnUpdatedValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var unexpectedResult = "unexpectedResult";
            var cacheKey = "key";
            var cache = new SaintModeCache();
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10) };
            cache.GetOrUpdate(cacheKey, key => unexpectedResult, policy);
            Thread.Sleep(20);
            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);
            cache.GetOrUpdate(cacheKey, key =>
            {
                ewh.Set();
                return expectedResult;
            }, ObjectCache.InfiniteAbsoluteExpiration);

            // act
            ewh.WaitOne();
            var cacheValue = cache.GetOrUpdate(cacheKey, key => expectedResult, policy);

            // assert
            Assert.That(cacheValue, Is.EqualTo(expectedResult));
        }

        [Test]
        public void WhenNoCacheMultipleThreadsRequestValueThenOnlyOneCreatesValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            var cache = new SaintModeCache();
            int counter = 0;
            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);

            // act //assert
            for (var i=0; i< 100000; i++)
            {
                Func<object> testCache = () => cache.GetOrUpdate(cacheKey, key =>
                {
                    ewh.WaitOne();
                    Interlocked.Increment(ref counter);
                    return expectedResult;
                });

                testCache.BeginInvoke(re =>
                {
                    var result = testCache.EndInvoke(re);
                    Assert.That(result, Is.EqualTo(expectedResult));
                    Assert.That(counter, Is.EqualTo(1));
                }, testCache);
            }

            ewh.Set();
        }

        [Test]
        public void WhenCacheExpiresMultipleThreadsRequestValueThenOnlyOneCreatesValue()
        {
            // arrange
            var expectedResult = "expectedResult";
            var cacheKey = "key";
            var cache = new SaintModeCache();
            var policy = new CacheItemPolicy { AbsoluteExpiration = DateTime.UtcNow.AddMilliseconds(10) };
            cache.GetOrUpdate(cacheKey, key => expectedResult, policy);
            Thread.Sleep(20);
            int counter = 0;
            var ewh = new EventWaitHandle(false, EventResetMode.ManualReset);

            // act //assert
            for (var i = 0; i < 100000; i++)
            {
                Func<object> testCache = () =>
                {
                    ewh.WaitOne();
                    return cache.GetOrUpdate(cacheKey, key =>
                    {
                        Interlocked.Increment(ref counter);
                        return expectedResult;
                    }, policy);
                };

                testCache.BeginInvoke(re =>
                {
                    var result = testCache.EndInvoke(re);
                    Assert.That(result, Is.EqualTo(expectedResult));
                    Assert.That(counter, Is.LessThanOrEqualTo(1));
                }, testCache);
            }

            ewh.Set();
        }
    }
}
