using BitMiracle.LibTiff.Classic;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using WebApplication1.Models;

namespace WebApplication1.Services
{
    /// <summary>
    /// Serwis obsługujący progi NDVI – ładuje dane z bazy, cache’uje i udostępnia metodę GetThreshold.
    /// </summary>
    public class ThresholdService : BackgroundService
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger<ThresholdService> _logger;
        private readonly ThresholdStore _store;

        public ThresholdService(DatabaseService databaseService, ILogger<ThresholdService> logger, ThresholdStore store)
        {
            _databaseService = databaseService;
            _store = store;
            _logger = logger;
        }

        /// <summary>
        /// Zwraca wartość progową NDVI dla danej rośliny i dat.
        /// </summary>


        private async Task RefreshCacheAsync()
        {
            var thresholds = await _databaseService.GetThresholdsAsync();
            _store.Update(thresholds);
            _logger.LogInformation("Threshold cache refreshed at {time}, loaded {count} records", DateTime.Now, thresholds.Count);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RefreshCacheAsync();

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                await RefreshCacheAsync();
            }
        }
    }
}
