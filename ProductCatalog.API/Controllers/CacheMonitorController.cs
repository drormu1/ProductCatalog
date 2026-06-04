using Microsoft.AspNetCore.Mvc;
using ProductCatalog.Infrastructure.Caching;
using ProductCatalog.Infrastructure.Repositories;
using System.Text;

namespace ProductCatalog.API.Controllers;

[ApiController]
public class CacheMonitorController : ControllerBase
{
    private readonly IProductCache _cache;
    private readonly IProductRepository _repository;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<CacheMonitorController> _logger;

    public CacheMonitorController(
        IProductCache cache,
        IProductRepository repository,
        IWebHostEnvironment environment,
        ILogger<CacheMonitorController> logger)
    {
        _cache = cache;
        _repository = repository;
        _environment = environment;
        _logger = logger;
    }

    [HttpGet("api/monitor/cache-stats")]
    public ActionResult<CacheStats> GetStats()
    {
        var stats = _cache.GetStats();
        stats.CreateWaitCount = _repository.GetCreateWaitCount();
        stats.DuplicateSkuCount = _repository.GetDuplicateSkuCount();
        stats.RepositoryProductIds = _repository.GetProductIds();
        return Ok(stats);
    }

    [HttpGet("monitor")]
    public async Task<ContentResult> Monitor()
    {
        var templatePath = Path.Combine(_environment.ContentRootPath, "HtmlTemplates", "cache-monitor.html");

        if (!System.IO.File.Exists(templatePath))
        {
            _logger.LogWarning("Monitor template file not found at {Path}", templatePath);
            return Content("<h3>Monitor template not found.</h3>", "text/html");
        }

        var html = await System.IO.File.ReadAllTextAsync(templatePath, Encoding.UTF8);
        return Content(html, "text/html; charset=utf-8");
    }
}
