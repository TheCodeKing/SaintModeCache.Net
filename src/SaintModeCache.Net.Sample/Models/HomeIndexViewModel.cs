using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web.Mvc;

namespace SaintModeCache.Net.Sample.Models
{
    public class HomeIndexViewModel
    {
        public IEnumerable<CacheDetailsModel> Items { get; set; }
    }
}