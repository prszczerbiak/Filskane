using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using System.IO;
using System.Text.Json;

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
            Console.WriteLine(bboxJson);
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
            Console.WriteLine(typeof(SixLabors.ImageSharp.Image<Rgb24>).Assembly.FullName);
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

        public static byte[] CreateRiskOverlay(double[][] input, int[] classes, int width, int height, float opacity = 0.4f, int scale = 6)
        {
            int newWidth = width * scale;
            int newHeight = height * scale;

            using var image = new Image<Rgba32>(newWidth, newHeight);
            image.Mutate(ctx => ctx.Fill(new Rgba32(255, 255, 255, 0)));

            for(int i = 0;i< input.Length; i++)
            {
                int cls = classes[i];
                int x = (int)input[i][0];
                int y = (int)input[i][1];

                Rgba32 color = cls switch
                {
                    0 => new Rgba32(0, 255, 0, (byte)(opacity * 255)),
                    1 => new Rgba32(255, 255, 0, (byte)(opacity * 255)),
                    2 => new Rgba32(255, 0, 0, (byte)(opacity * 255)),
                    _ => new Rgba32(0, 0, 0, 0)
                };

                for (int dy = 0; dy < scale; dy++)
                {
                    for (int dx = 0; dx < scale; dx++)
                    {
                        image[x * scale + dx, y * scale + dy] = color;
                    }
                }
            }

            //for (int y = 0; y < height; y++)
            //{
            //    for (int x = 0; x < width; x++)
            //    {
            //        int cls = classes[y, x];

            //        Rgba32 color = cls switch
            //        {
            //            0 => new Rgba32(0, 255, 0, (byte)(opacity * 255)),
            //            1 => new Rgba32(255, 255, 0, (byte)(opacity * 255)),
            //            2 => new Rgba32(255, 0, 0, (byte)(opacity * 255)),
            //            _ => new Rgba32(0, 0, 0, 0)
            //        };

            //        // wypełnienie bloku scale x scale
            //        for (int dy = 0; dy < scale; dy++)
            //        {
            //            for (int dx = 0; dx < scale; dx++)
            //            {
            //                image[x * scale + dx, y * scale + dy] = color;
            //            }
            //        }
            //    }
            //}

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
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
    }
}
