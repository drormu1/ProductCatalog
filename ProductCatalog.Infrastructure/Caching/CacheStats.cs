namespace ProductCatalog.Infrastructure.Caching;

public class CacheStats
{
    public long HitCount { get; set; }
    public long MissCount { get; set; }
    public long WaitCount { get; set; }
    public long SetCount { get; set; }
    public long RefreshCount { get; set; }
    public long RemoveCount { get; set; }
    public long ExpiredCount { get; set; }
    public long CreateWaitCount { get; set; }
    public long DuplicateSkuCount { get; set; }
    public int CurrentSize { get; set; }

    public double HitRate => HitCount + MissCount == 0
        ? 0
        : Math.Round((double)HitCount / (HitCount + MissCount) * 100, 2);
}
