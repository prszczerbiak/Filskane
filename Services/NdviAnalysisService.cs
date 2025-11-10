using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using System.Security.Cryptography;
using WebApplication1.Analysis;
using WebApplication1.Models;
using WebApplication1.Services.Agronomy;
using WebApplication1.Utils;
using static SkiaSharp.HarfBuzz.SKShaper;

namespace WebApplication1.Services
{
    public class NdviAnalysisService
    {
        private readonly CropThresholdProvider _thresholdProvider;

        public NdviAnalysisService(CropThresholdProvider thresholdProvider)
        {
            _thresholdProvider = thresholdProvider;
        }

        public byte[] GroupByRisk(NdviGroupRequest request, byte[] tiffFile, string geoJsonPolygon, string bboxJson)
        {
            // 🔹 1. Oblicz macierz NDVI z pliku TIFF
            double[,] ndvi = ScanResult.CalculateNdvi(tiffFile);

            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);

            double[][] fieldNvdi = ImageUtils.GetPixelsInsidePolygon(width, height, geoJsonPolygon, bboxJson);

            var classyficator = new NdviClassifier(_thresholdProvider);
            int[] labels = classyficator.Classify(fieldNvdi, ndvi, request.CropType,request.SowingDate);

            // 🔹 2. Utwórz i wytrenuj model
            var model = new BayesNDVI();
            model.Train(fieldNvdi, labels);

            // 🔹 3. Wykonaj predykcję (klasy ryzyka)
            int[] classes = model.Predict(fieldNvdi);

            // 🔹 4. Zamień wynikową macierz klas na obraz (lub inny format)
            // Tu przykład: konwersja do GeoTIFF, PNG albo bajtowej reprezentacji.
            byte[] overlayBytes = ImageUtils.CreateRiskOverlay(fieldNvdi,classes, width, height, 0.5f);

            return overlayBytes;
        }


    }
}
