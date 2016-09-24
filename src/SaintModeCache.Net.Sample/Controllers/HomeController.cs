using System;
using System.Collections.Generic;
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
            var model = new HomeIndexViewModel();
            var details = new List<CacheDetailsModel>();

            Cache.GetOrCreate("5SecondServiceKey", k =>
            {
                Thread.Sleep(5000);
                return string.Concat("This text takes 5 seconds to load, and expires in 10 seconds. Updated ",
                    DateTime.UtcNow.ToLongTimeString());
            },
                DateTime.UtcNow.AddSeconds(10));

            Cache.GetOrCreate("1SecondServiceKey", k =>
            {
                Thread.Sleep(1000);
                return string.Concat("This text takes 1 seconds to load, and expires in 20 seconds. Updated ",
                    DateTime.UtcNow.ToLongTimeString());
            },
                DateTime.UtcNow.AddSeconds(20));

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

            model.Items = details;
            return View(model);
        }
    }
}