using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System.Security.Cryptography;
using WebApplication1.Models;
using WebApplication1.Services.Agronomy;

namespace WebApplication1.Services
{
    public class NdviAnalysisService
    {
        private readonly ThresholdSelector _thresholdSelector;

        public NdviAnalysisService(ThresholdSelector thresholdSelector)
        {
            _thresholdSelector = thresholdSelector;
        }

        public byte[] GroupByRisk(NdviGroupRequest request, byte[] tiffFile)
        {
            // 🔹 1. Pobierz dane NDVI (tu na razie mock)
            var pixels = ScanResult.CalculateNdvi(tiffFile);

            // 🔹 2. Oblicz próg NDVI dla rośliny i daty
            double threshold = _thresholdSelector.GetThreshold(request.CropType, request.SowingDate, DateTime.Now);

            // 🔹 3. Oznacz piksele zagrożone

            return null;
            
        }

        
    }
}
