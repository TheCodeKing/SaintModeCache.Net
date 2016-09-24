namespace SaintModeCache.Net.Sample.Models
{
    public class CacheDetailsModel
    {
        public string CacheKey { get; set; }
        public double NextUpdateSeconds { get; set; }
        public bool Stale { get; set; }
        public string Value { get; set; }
    }
}