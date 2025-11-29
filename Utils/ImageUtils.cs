using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System.IO;
using System.Text.Json;
using SixLabors.Fonts;

namespace WebApplication1.Utils
{
    public class ImageUtils
    {
        public static byte[] DrawGeoJsonPolygonOnImage(
        byte[] imageBytes,        // PNG lub TIFF w pamięci (tu PNG po konwersji)
        string geoJsonPolygon,    // Twój GeoJSON
        string bboxJson,
        bool isHeatmap)          // {"minX":..., "minY":..., "maxX":..., "maxY":...}
        {
            // --- Parsowanie obrazu ---
            using var msImg = new MemoryStream(imageBytes);
            using var image = Image.Load<Rgb24>(msImg);

            int width = image.Width;
            int height = image.Height;

            // --- Parsowanie BBox ---
            var bbox = JsonSerializer.Deserialize<BBox>(bboxJson);
            if (bbox == null)
            {
                // brak BBox → zwracamy obraz bez polygonu
                Console.WriteLine("Bbox jest null");
                return imageBytes;
            }
            double minX = bbox.minX;
            double minY = bbox.minY;
            double maxX = bbox.maxX;
            double maxY = bbox.maxY;

            // --- Parsowanie GeoJSON ---
            using var doc = JsonDocument.Parse(geoJsonPolygon);
            var coords = doc.RootElement
                            .GetProperty("geometry")
                            .GetProperty("coordinates")[0]; // pierwsza (zewnętrzna) linia polygonu

            var points = new List<PointF>();

            foreach (var point in coords.EnumerateArray())
            {
                double x = point[0].GetDouble(); // longitude
                double y = point[1].GetDouble(); // latitude

                if (x < minX || x > maxX || y < minY || y > maxY)
                {
                    // Jeden punkt poza zakresem → zwracamy obraz bez polygonu
                    Console.WriteLine("Zjdęcie nie pasuje do polygonu");
                    return imageBytes;
                }

                float px, py;
                if (isHeatmap)
                {
                    px = ((float)((x - minX) / (maxX - minX) * width));
                    py = ((float)((maxY - y) / (maxY - minY) * height)); // Y odwrotnie, bo piksele rosną w dół
                }
                else
                {
                    px = ((float)((x - minX) / (maxX - minX) * width));
                    py = ((float)((maxY - y) / (maxY - minY) * height));
                }
                    points.Add(new PointF(px, py));
            }
            // --- Rysowanie polygonu ---

            float thickness = 0.5f;

            if (isHeatmap)
                thickness = 2.0f;
            image.Mutate(ctx => ctx.DrawPolygon(Color.Red, thickness, points.ToArray()));



            // --- Zapis PNG do pamięci ---
            using var outMs = new MemoryStream();
            image.SaveAsPng(outMs);
            return outMs.ToArray();
        }

        public static byte[] CreateRiskOverlay(double[][] input, int[] combinedLabels, int width, int height, float opacity = 0.4f, int scale = 6)
        {
            int newWidth = width * scale;
            int newHeight = height * scale;

            using var image = new Image<Rgba32>(newWidth, newHeight);
            image.Mutate(ctx => ctx.Fill(new Rgba32(255, 255, 255, 0)));

            for (int i = 0; i < input.Length; i++)
            {
                int label = combinedLabels[i];
                int x = (int)input[i][0];
                int y = (int)input[i][1];

                if (x < 0 || x >= width || y < 0 || y >= height)
                    continue;

                Rgba32 color = label switch
                {
                    0 => new Rgba32(0, 255, 0, (byte)(opacity * 255)),     // zielony
                    1 => new Rgba32(255, 255, 0, (byte)(opacity * 255)),   // żółty (szum)
                    _ => GetSmoothClusterColor(label)              // 🔹 SKALOWANA CZERWIEŃ
                };

                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        int pixelX = x * scale + dx;
                        int pixelY = y * scale + dy;

                        if (pixelX >= 0 && pixelX < newWidth && pixelY >= 0 && pixelY < newHeight)
                        {
                            image[pixelX, pixelY] = color;
                        }
                    }
                }
            }

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        // 🔹 SKALOWANA CZERWIEŃ WG NUMERU KLASTRA
        private static Rgba32 GetSmoothClusterColor(int clusterId)
        {
            // 🔹 SKALOWANIE: clusterId = -1, -2, -3, -4...
            // -1 = najciemniejszy, potem coraz jaśniejsze
            int baseIntensity = 80;  // bardzo ciemny czerwony dla -1
            int intensityStep = 25;  // krok rozjaśniania

            // Dla -1: 80, dla -2: 105, dla -3: 130, itd.
            int intensity = Math.Min(255, baseIntensity + (Math.Abs(clusterId + 1)) * intensityStep);

            return new Rgba32((byte)intensity, 0, 0, 255);
        }

        public static byte[] ApplyRiskOverlay(byte[] baseImage, byte[] overlayImage)
        {
            using var msBase = new MemoryStream(baseImage);
            using var baseImg = Image.Load<Rgba32>(msBase);
            using var msOv = new MemoryStream(overlayImage);
            using var overlay = Image.Load<Rgba32>(msOv);
           
            baseImg.Mutate(ctx => ctx.DrawImage(overlay, 1f));

            using var msOut = new MemoryStream();
            baseImg.SaveAsPng(msOut);
            return msOut.ToArray();
        }
        public class BBox
        {
            public double minX { get; set; }
            public double minY { get; set; }
            public double maxX { get; set; }
            public double maxY { get; set; }
        }

        public static double[][] GetPixelsInsidePolygon(
            int imageWidth,
            int imageHeight,
            string geoJsonPolygon,
            string bboxJson)
        {
            // --- Parsowanie BBox ---
            var bbox = JsonSerializer.Deserialize<BBox>(bboxJson);
            if (bbox == null) return Array.Empty<double[]>();

            double minX = bbox.minX;
            double minY = bbox.minY;
            double maxX = bbox.maxX;
            double maxY = bbox.maxY;

            // --- Parsowanie GeoJSON ---
            using var doc = JsonDocument.Parse(geoJsonPolygon);
            var coords = doc.RootElement
                            .GetProperty("geometry")
                            .GetProperty("coordinates")[0]; // pierwsza (zewnętrzna) linia polygonu

            var polygonPoints = new List<PointF>();
            foreach (var point in coords.EnumerateArray())
            {
                double x = point[0].GetDouble();
                double y = point[1].GetDouble();

                // przeskalowanie do pikseli
                float px = (float)((x - minX) / (maxX - minX) * imageWidth);
                float py = (float)((maxY - y) / (maxY - minY) * imageHeight); // Y odwrotnie
                polygonPoints.Add(new PointF(px, py));
            }

            // --- Tworzymy listę punktów wewnątrz polygonu ---
            var insidePoints = new List<double[]>();

            // Bounding box polygonu w pikselach - przyspiesza działanie
            int minPixelX = (int)Math.Floor(polygonPoints.Min(p => p.X));
            int maxPixelX = (int)Math.Ceiling(polygonPoints.Max(p => p.X));
            int minPixelY = (int)Math.Floor(polygonPoints.Min(p => p.Y));
            int maxPixelY = (int)Math.Ceiling(polygonPoints.Max(p => p.Y));

            // Iteracja tylko po prostokącie ograniczającym polygon
            for (int y = minPixelY; y <= maxPixelY; y++)
            {
                if (y < 0 || y >= imageHeight) continue;
                for (int x = minPixelX; x <= maxPixelX; x++)
                {
                    if (x < 0 || x >= imageWidth) continue;
                    if (IsPointInPolygon(new PointF(x, y), polygonPoints))
                    {
                        insidePoints.Add(new double[] { x, y });
                    }
                }
            }

            return insidePoints.ToArray();
        }

        // --- Ray-casting algorithm ---
        private static bool IsPointInPolygon(PointF p, List<PointF> polygon)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > p.Y) != (polygon[j].Y > p.Y)) &&
                     (p.X < (polygon[j].X - polygon[i].X) * (p.Y - polygon[i].Y) /
                     (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }


        public static List<List<double>> ConvertToNestedList(double[,] array)
        {
            int height = array.GetLength(0);
            int width = array.GetLength(1);

            var result = new List<List<double>>(height);
            for (int y = 0; y < height; y++)
            {
                var row = new List<double>(width);
                for (int x = 0; x < width; x++)
                    row.Add(array[y, x]);
                result.Add(row);
            }

            return result;
        }

        public static double[,] ConvertFromNestedList(List<List<double>> list)
        {
            int height = list.Count;
            int width = list[0].Count;

            var result = new double[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                    result[y, x] = list[y][x];
            }

            return result;
        }

        public static void SaveArrayToTxt(double[,] array, string filePath)
        {
            int height = array.GetLength(0);
            int width = array.GetLength(1);

            using (var writer = new StreamWriter(filePath))
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        writer.Write(array[y, x]);

                        if (x < width - 1)
                            writer.Write(" "); // separator między wartościami
                    }
                    writer.WriteLine();
                }
            }
        }

        public static byte[] CreateLegend(Dictionary<string, double> ndviMedians, int[] clusterIdsPresent, bool darkMode = false)
        {
            int width = 400;
            int height = 300;

            using var image = new Image<Rgba32>(width, height);

            // 🔹 KOLORY W ZALEŻNOŚCI OD TRYBU
            Rgba32 backgroundColor = darkMode ? new Rgba32(42, 43, 46, 255) : new Rgba32(255, 255, 255, 255);
            Rgba32 textColor = darkMode ? new Rgba32(240, 240, 240, 255) : new Rgba32(0, 0, 0, 255);
            Rgba32 borderColor = darkMode ? new Rgba32(100, 100, 100, 255) : new Rgba32(0, 0, 0, 255);

            image.Mutate(ctx => ctx.Fill(backgroundColor));

            var items = new List<(string Label, Rgba32 Color)>();

            // 🔹 PODSTAWOWE KOLORY (niezależne od trybu)
            items.Add(("NDVI bardzo dobre", new Rgba32(0, 255, 0, 255)));
            items.Add(("NDVI zadowalające", new Rgba32(255, 255, 0, 255)));

            // 🔹 KLASTRY CZERWONE
            if (clusterIdsPresent != null && ndviMedians != null)
            {
                var redClusters = clusterIdsPresent
                    .Where(id => id < 0)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList();

                foreach (var clusterId in redClusters)
                {
                    string clusterKey = clusterId.ToString();
                    double median = ndviMedians.ContainsKey(clusterKey) ? ndviMedians[clusterKey] : 0.0;
                    string label = $"Mediana: {median:F3}";
                    Rgba32 color = GetSmoothClusterColor(clusterId);
                    items.Add((label, color));
                }
            }

            // 🔹 RYSUJ LEGENDĘ
            int itemHeight = 25;
            int startY = 40;
            int colorBoxSize = 20;
            int textLeft = 40;
            int textVerticalOffset = 4;

            for (int i = 0; i < items.Count; i++)
            {
                int y = startY + i * itemHeight;

                // Prostokąt z kolorem
                image.Mutate(ctx => ctx.FillPolygon(
                    items[i].Color,
                    new PointF[] {
                new PointF(15, y),
                new PointF(15 + colorBoxSize, y),
                new PointF(15 + colorBoxSize, y + colorBoxSize),
                new PointF(15, y + colorBoxSize)
                    }));

                // Tekst z kolorem odpowiednim dla trybu
                image.Mutate(ctx => ctx.DrawText(
                    items[i].Label,
                    SystemFonts.CreateFont("Arial", 12),
                    textColor,
                    new PointF(textLeft, y + textVerticalOffset)));
            }

            // 🔹 TYTUŁ
            image.Mutate(ctx => ctx.DrawText(
                "Legenda - Analiza Ryzyka NDVI",
                SystemFonts.CreateFont("Arial", 14, FontStyle.Bold),
                textColor,
                new PointF(10, 15)));


            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }
    }
}
