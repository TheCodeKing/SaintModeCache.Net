using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web.Mvc;
using Conditions;
using SaintModeCache.Net.Sample.Models;

namespace SaintModeCache.Net.Sample.Controllers
{
    public class HomeController : Controller
    {
        private static readonly SaintModeCaching.SaintModeCache Cache = new SaintModeCaching.SaintModeCache();

        private static string GetCacheableDataForExample1()
        {
            return Cache.GetOrCreate("Example1", (key, cancel) =>
            {
                Thread.Sleep(5000);
                return string.Concat("This text takes 5 seconds to load, and expires in 10 seconds. Updated ",
                    DateTime.UtcNow.ToLongTimeString());
            },
                DateTime.UtcNow.AddSeconds(10));
        }

        private static string GetCacheableDataForExample2()
        {
            return Cache.GetOrCreate("Example2", (key, cancel) =>
            {
                Thread.Sleep(1000);
                return string.Concat("This text takes 1 second to load, and expires in 20 seconds. Updated ",
                    DateTime.UtcNow.ToLongTimeString());
            },
                DateTime.UtcNow.AddSeconds(20));
        }

        private static string GetCacheableDataForExample3()
        {
            return Cache.GetOrCreate("Example3", (key, cancel) =>
            {
                Thread.Sleep(5000);
                var gen = new Random((int) DateTime.UtcNow.Ticks);
                var prob = gen.Next(100);
                cancel.IsCancellationRequested = prob > 30;
                return string.Concat("This text randomly fails to update. Expires after 5 seconds. Updated ",
                    DateTime.UtcNow.ToLongTimeString());
            },
                DateTime.UtcNow.AddSeconds(5));
        }

        public ActionResult Index()
        {
            var model = new HomeIndexViewModel();
            var details = new List<CacheDetailsModel>();

            var exampleResult1 = GetCacheableDataForExample1();
            exampleResult1.Ensures("exampleResult1").IsNotNull();

            var exampleResult2 = GetCacheableDataForExample2();
            exampleResult2.Ensures("exampleResult2").IsNotNull();

            EnsureInitalValueForExample3AsItSupportsCancelUpdate();
            var exampleResult3 = GetCacheableDataForExample3();
            exampleResult3.Ensures("exampleResult3").IsNotNull();

            foreach (var item in Cache)
            {
                var modelDetails = new CacheDetailsModel
                {
                    Value = item.Value as string,
                    Stale = Cache.Stale(item.Key),
                    CacheKey = item.Key
                };

                details.Add(modelDetails);
            }

            model.Items = details.OrderBy(x => x.CacheKey);
            return View(model);
        }

        private void EnsureInitalValueForExample3AsItSupportsCancelUpdate()
        {
            object currentItem;
            if (!Cache.TryGet("Example3", out currentItem))
            {
                Cache.SetOrUpdateWithoutCreate("Example3",
                    "Default value until I get data. Expire in 5 seconds.",
                    DateTime.UtcNow.AddSeconds(5));
            }
        }
    }
}