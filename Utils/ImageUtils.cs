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

        // Klasa pomocnicza do JSON bbox
        public class BBox
        {
            public double minX { get; set; }
            public double minY { get; set; }
            public double maxX { get; set; }
            public double maxY { get; set; }
        }
    }
}
