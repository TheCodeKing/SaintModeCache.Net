using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace SaintModeCache.Net.Sample.Models
{
    public class HomeIndexViewModel
    {
        public string CacheKey { get; set; }
        public double NextUpdateSeconds { get; set; }
        public string Value { get; set; }
        public bool Stale { get; set; }
    }
}