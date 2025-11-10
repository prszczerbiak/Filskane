namespace WebApplication1.Analysis
{
    public class CropThresholdProvider
    {
        // Przykładowe progi NDVI (można potem uczynić dynamicznymi)
        private static readonly Dictionary<string, (double Good, double Medium)> CropThresholds = new()
        {
            { "pszenica", (0.6, 0.4) },
            { "kukurydza", (0.7, 0.5) },
            { "rzepak", (0.65, 0.45) },
            { "jęczmień", (0.58, 0.38) },
            { "żyto", (0.55, 0.35) },
            { "burak cukrowy", (0.68, 0.48) }
        };

        public (double Good, double Medium) GetThresholds(string cropType, DateTime sowingDate, DateTime scanDate)
        {
            if (CropThresholds.TryGetValue(cropType.ToLower(), out var thresholds))
                return thresholds;

            // domyślne wartości
            return (0.6, 0.4);
        }
    }
}
