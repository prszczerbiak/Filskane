using NetVips;
using WebApplication1.Models;
using System.Collections.Concurrent;

namespace WebApplication1.Services
{
    public class ThresholdStore
    {
        private ConcurrentDictionary<int, ThresholdDto> _thresholds = new();
        public ConcurrentDictionary<int, ThresholdDto> Thresholds => _thresholds;

        public void Update(IEnumerable<ThresholdDto> thresholds)
        {
            var newCache = new ConcurrentDictionary<int, ThresholdDto>();
            foreach (var t in thresholds)
                newCache[t.CycleId] = t;

            _thresholds = newCache; // atomowa wymiana cache
        }

        public (double Lower, double Upper) GetThreshold(int cycleId)
        {
            if (_thresholds.TryGetValue(cycleId, out var threshold))
                return (threshold.MinNdvi, threshold.MaxNdvi);

            return (0.4, 0.6); // fallback
        }
    }
}
