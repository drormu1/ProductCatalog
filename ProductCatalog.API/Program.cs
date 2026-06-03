using System.Text.Json;
using System.Diagnostics;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.Logging.Console;
using ProductCatalog.API.Services;
using ProductCatalog.Infrastructure.Caching;
using ProductCatalog.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
    options.ColorBehavior = LoggerColorBehavior.Enabled;
});

builder.Services.AddControllers();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
});
builder.Services.AddMemoryCache(options =>
{
    options.ExpirationScanFrequency = TimeSpan.FromSeconds(1);
});
builder.Services.Configure<ProductCacheSettings>(builder.Configuration.GetSection("CacheSettings"));
builder.Services.AddSingleton<IProductRepository, InMemoryProductRepository>();
builder.Services.AddSingleton<IProductCache, MemoryProductCache>();
builder.Services.AddScoped<IProductService, ProductService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //  app.UseSwagger();
    //  app.UseSwaggerUI();

    OpenMonitorInBrowserOnStart(app);
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseHttpLogging();

app.UseAuthorization();

app.MapControllers();

app.Logger.LogInformation("Application started. Cache size={CacheSize}", 0);

app.Run();

static void OpenMonitorInBrowserOnStart(WebApplication app)
{
    var autoOpenMonitor = app.Configuration.GetValue<bool?>("Monitor:AutoOpenBrowser") ?? true;
    if (autoOpenMonitor)
    {
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var address = app.Urls.FirstOrDefault(x => x.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrWhiteSpace(address))
            {
                return;
            }

            var monitorUrl = $"{address.TrimEnd('/')}/monitor";

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = monitorUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                app.Logger.LogWarning("Could not auto-open monitor URL: {MonitorUrl}", monitorUrl);
            }
        });
    }
}