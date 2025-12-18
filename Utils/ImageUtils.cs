using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using BitMiracle.LibTiff.Classic;
using SixLabors.Fonts; // Jeśli używasz legendy z tekstem

namespace WebApplication1.Utils;

public static class ImageUtils
{
    // --- 1. KONWERSJE FORMATÓW ---

    public static byte[] ConvertTiffToPng(byte[] tiffBytes)
    {
        using var inputStream = new MemoryStream(tiffBytes);
        using var tiff = Tiff.ClientOpen("in-mem", "r", inputStream, new TiffStream());

        if (tiff == null) throw new Exception("Błąd odczytu TIFF");

        int w = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        int h = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        int samples = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
        int scanlineSize = tiff.ScanlineSize();
        byte[] scanline = new byte[scanlineSize];

        using var image = new Image<Rgb24>(w, h);

        for (int y = 0; y < h; y++)
        {
            tiff.ReadScanline(scanline, y);
            for (int x = 0; x < w; x++)
            {
                int offset = x * samples * 2;
                // Sentinel-2 (RGB): B04(Red), B03(Green), B02(Blue)
                // Offsety: 0=Blue, 2=Green, 4=Red
                ushort b = BitConverter.ToUInt16(scanline, offset + 0);
                ushort g = BitConverter.ToUInt16(scanline, offset + 2);
                ushort r = BitConverter.ToUInt16(scanline, offset + 4);

                // Normalizacja dla wyświetlania (rozjaśnienie)
                image[x, y] = new Rgb24(
                    (byte)(Math.Min(r * 255f / 16383, 255)),
                    (byte)(Math.Min(g * 255f / 16383, 255)),
                    (byte)(Math.Min(b * 255f / 16383, 255))
                );
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    // --- 2. RYSOWANIE I EDYCJA ---

    public static byte[] DrawGeoJsonPolygonOnImage(byte[] imageBytes, string geoJson, string bboxJson, bool isThick)
    {
        using var image = Image.Load(imageBytes);
        var bbox = GeoUtils.ParseBbox(bboxJson);

        if (bbox != null)
        {
            var points = GeoUtils.GetPolygonPixels(geoJson, bbox, image.Width, image.Height);
            if (points.Count > 2)
            {
                float thickness = isThick ? 1.5f : 0.5f;
                // Rysujemy czerwony obrys
                image.Mutate(ctx => ctx.DrawPolygon(Color.Red, thickness, points.ToArray()));
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static byte[] CombineImages(byte[] baseBytes, byte[] overlayBytes)
    {
        using var baseImg = Image.Load<Rgba32>(baseBytes);
        using var overlay = Image.Load<Rgba32>(overlayBytes);

        // Nakłada overlay na baseImg z przezroczystością (jeśli overlay ma alpha)
        baseImg.Mutate(ctx => ctx.DrawImage(overlay, 1f));

        using var ms = new MemoryStream();
        baseImg.SaveAsPng(ms);
        return ms.ToArray();
    }

    // --- 3. LOGIKA KLASTRÓW I RYZYKA (dla GroupByRisk) ---

    public static byte[] GenerateRiskOverlay(double[,] ndviMatrix, double minT, double maxT)
    {
        int h = ndviMatrix.GetLength(0);
        int w = ndviMatrix.GetLength(1);

        // Upscaling, żeby pasowało do heatmapy z PlotUtils (tam daliśmy x6)
        int scale = 6;
        int newW = w * scale;
        int newH = h * scale;

        using var image = new Image<Rgba32>(newW, newH);

        // Wypełniamy przezroczystością
        image.Mutate(ctx => ctx.Fill(new Rgba32(0, 0, 0, 0)));

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double val = ndviMatrix[y, x];
                Rgba32 color = new Rgba32(0, 0, 0, 0);

                if (val < minT)
                    color = new Rgba32(255, 0, 0, 100); // Czerwony (krytyczne), półprzezroczysty
                else if (val >= minT && val < maxT)
                    color = new Rgba32(255, 255, 0, 100); // Żółty (ostrzegawczy)

                if (color.A > 0)
                {
                    // Rysujemy kwadrat NxN pikseli (skalowanie)
                    for (int dy = 0; dy < scale; dy++)
                        for (int dx = 0; dx < scale; dx++)
                            image[x * scale + dx, y * scale + dy] = color;
                }
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static byte[] GenerateLegend(double minT, double maxT)
    {
        // Prosta legenda generowana jako obrazek
        int w = 300, h = 100;
        using var image = new Image<Rgba32>(w, h);
        image.Mutate(ctx => ctx.Fill(Color.White));

        var font = SystemFonts.CreateFont("Arial", 14);

        image.Mutate(ctx => {
            // Czerwony
            ctx.Fill(Color.Red, new RectangleF(10, 10, 20, 20));
            ctx.DrawText($"Ryzyko wysokie (< {minT:F2})", font, Color.Black, new PointF(40, 10));

            // Żółty
            ctx.Fill(Color.Yellow, new RectangleF(10, 40, 20, 20));
            ctx.DrawText($"Ryzyko średnie ({minT:F2} - {maxT:F2})", font, Color.Black, new PointF(40, 40));

            // Zielony
            ctx.Fill(Color.Green, new RectangleF(10, 70, 20, 20));
            ctx.DrawText($"Zdrowe (> {maxT:F2})", font, Color.Black, new PointF(40, 70));
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    // --- 4. POMOCNICZE (List <-> Array) ---

    public static List<List<double>> ConvertToNestedList(double[,] array)
    {
        int h = array.GetLength(0);
        int w = array.GetLength(1);
        var list = new List<List<double>>(h);
        for (int y = 0; y < h; y++)
        {
            var row = new List<double>(w);
            for (int x = 0; x < w; x++) row.Add(array[y, x]);
            list.Add(row);
        }
        return list;
    }

    public static List<List<double>> ConvertToNestedList(float[,] array)
    {
        int h = array.GetLength(0);
        int w = array.GetLength(1);
        var list = new List<List<double>>(h);
        for (int y = 0; y < h; y++)
        {
            var row = new List<double>(w);
            for (int x = 0; x < w; x++) row.Add(array[y, x]);
            list.Add(row);
        }
        return list;
    }

    public static double[,] ConvertFromNestedList(List<List<double>> list)
    {
        int h = list.Count;
        int w = list[0].Count;
        var arr = new double[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                arr[y, x] = list[y][x];
        return arr;
    }

    public static byte[] CreateRiskOverlayFromPoints(double[][] points, int[] labels, int width, int height)
    {
        // Upscaling x6 dla lepszej jakości (tak jak w heatmapie)
        int scale = 6;
        int newW = width * scale;
        int newH = height * scale;

        using var image = new Image<Rgba32>(newW, newH);
        image.Mutate(ctx => ctx.Fill(new Rgba32(0, 0, 0, 0))); // Przezroczyste tło

        for (int i = 0; i < points.Length; i++)
        {
            int x = (int)points[i][0];
            int y = (int)points[i][1];
            int label = labels[i];

            // Wybierz kolor na podstawie etykiety
            Rgba32 color;
            if (label == 0) color = new Rgba32(0, 255, 0, 100); // Zielone (dobre) - nie rysujemy nic (przezroczyste)
            else if (label == 1) color = new Rgba32(255, 255, 0, 100); // Żółty (średnie)
            else if (label == -1) color = new Rgba32(255, 0, 0, 100); // Szum z DBSCAN (Czerwony)
            else
            {
                // Klastry z Pythona (ID < -1, np. -2, -3...)
                // Generujemy odcienie czerwieni/fioletu dla różnych ognisk choroby
                color = GetClusterColor(label);
            }

            // Rysuj powiększony piksel
            for (int dy = 0; dy < scale; dy++)
                for (int dx = 0; dx < scale; dx++)
                    image[x * scale + dx, y * scale + dy] = color;
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    // Pomocnicza do kolorów klastrów
    private static Rgba32 GetClusterColor(int clusterId)
    {
        // clusterId z Pythona to np. 0, 1, 2... 
        // Ale my w AnalysisService zrobiliśmy mapowanie, żeby nie gryzły się z 0 i 1 (dobry/średni).
        // Załóżmy, że klastry to liczby ujemne < -1 lub duże dodatnie.

        // Prosta paleta dla klastrów (ognisk choroby):
        // Ciemna czerwień, Fiolet, Brąz
        int intensity = Math.Abs(clusterId) * 30;
        return new Rgba32((byte)(255 - (intensity % 100)), 0, (byte)((intensity * 2) % 255), 150);
    }

    // 2. Legenda dynamiczna (z klastrami)
    public static byte[] CreateLegendWithClusters(Dictionary<string, double> medians, int[] presentClusters, bool darkMode) // 1. Dodajemy parametr
    {
        int w = 400, h = 300; // Ewentualnie można zwiększyć wysokość, jeśli klastrów jest dużo
        using var image = new Image<Rgba32>(w, h);

        // 2. Definiujemy kolory w zależności od trybu
        // Ciemny szary (np. #2b2b2b) wygląda zazwyczaj lepiej niż czysty czarny
        Color bgColor = darkMode ? Color.ParseHex("#2b2b2b") : Color.White;
        Color textColor = darkMode ? Color.White : Color.Black;

        // 3. Wypełniamy tło
        image.Mutate(ctx => ctx.Fill(bgColor));

        var font = SystemFonts.CreateFont("Arial", 12);
        var titleFont = SystemFonts.CreateFont("Arial", 14, FontStyle.Bold);
        int y = 20;

        image.Mutate(ctx => {
            // Tytuł - używamy dynamicznego textColor
            ctx.DrawText("Legenda Ryzyka", titleFont, textColor, new PointF(10, y));
            y += 30;

            // Standardowe pozycje - przekazujemy textColor do metody pomocniczej
            DrawLegendItem(ctx, Color.Green, "Zdrowe (Wysokie NDVI)", ref y, font, textColor);
            DrawLegendItem(ctx, Color.Yellow, "Ostrzegawcze (Średnie NDVI)", ref y, font, textColor);
            DrawLegendItem(ctx, Color.Red, "Zagrożone (Niskie NDVI / Szum)", ref y, font, textColor);

            y += 10;
            // Nagłówek sekcji klastrów
            ctx.DrawText("Wykryte ogniska (DBSCAN):", SystemFonts.CreateFont("Arial", 12, FontStyle.Bold), textColor, new PointF(10, y));
            y += 25;

            // Klastry z Pythona
            foreach (var clusterId in presentClusters.Distinct())
            {
                string key = clusterId.ToString();
                double median = medians.ContainsKey(key) ? medians[key] : 0.0;

                // Metoda GetClusterColor musi być dostępna w klasie (mieliśmy ją wcześniej)
                Rgba32 color = GetClusterColor(clusterId);

                DrawLegendItem(ctx, color, $"Ognisko #{clusterId} (Mediana: {median:F2})", ref y, font, textColor);
            }
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    // === METODA POMOCNICZA (Zaktualizowana) ===
    private static void DrawLegendItem(
        IImageProcessingContext ctx,
        Color boxColor,
        string text,
        ref int y,
        Font font,
        Color textColor) // Dodajemy parametr koloru tekstu
    {
        // Rysujemy kwadracik koloru
        ctx.Fill(boxColor, new RectangleF(20, y, 20, 20));

        // Opcjonalnie: Jeśli tryb jest ciemny, a kolor kwadracika też ciemny (np. czarny/granatowy),
        // warto dodać jasną obwódkę:
        // if (textColor == Color.White) ctx.Draw(Color.Gray, 1, new RectangleF(20, y, 20, 20));

        // Rysujemy tekst odpowiednim kolorem
        ctx.DrawText(text, font, textColor, new PointF(50, y + 2));

        y += 30;
    }

    private static void DrawLegendItem(IImageProcessingContext ctx, Color color, string text, ref int y, Font font)
    {
        ctx.Fill(color, new RectangleF(20, y, 20, 20));
        ctx.DrawText(text, font, Color.Black, new PointF(50, y + 2));
        y += 30;
    }
}