using System;
using System.Threading;
using System.Web.Mvc;
using SaintModeCache.Net.Sample.Models;

namespace SaintModeCache.Net.Sample.Controllers
{
    public class HomeController : Controller
    {
        private static readonly SaintModeCaching.SaintModeCache Cache = new SaintModeCaching.SaintModeCache();

        public ActionResult Index()
        {
            var cacheKey = "ServiceKey";
            var model = new HomeIndexViewModel();
            var expireDateTime = DateTime.UtcNow.AddSeconds(10);
            model.Value = Cache.GetOrCreate("ServiceKey", k =>
            {
                Thread.Sleep(5000);
                return string.Concat("5 seconds to load this text. Last Updated ", DateTime.UtcNow);
            },
            expireDateTime);

            model.Stale = Cache.Stale(cacheKey);
            model.CacheKey = cacheKey;
            var lastUpdated = Cache.LastUpdatedDateTimeUtc(cacheKey);
            var nextUpdate = ((lastUpdated ?? expireDateTime) - DateTime.UtcNow).TotalSeconds;
            model.NextUpdateSeconds = nextUpdate;

            return View(model);
        }
    }
}