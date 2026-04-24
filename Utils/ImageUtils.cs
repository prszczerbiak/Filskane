using OSGeo.GDAL;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Filskane.Models;
using MaxRev.Gdal.Core;

namespace Filskane.Utils;

/// <summary>
/// Klasa pomocnicza w operacjach na obrazie png/tiff
/// </summary>
public static class ImageUtils
{
    private static readonly Rgba32 HealthyOverlayColor = new(0, 255, 0, 100);
    private static readonly Rgba32 WarningOverlayColor = new(255, 255, 0, 100);

    #region Public Methods
    /// <summary>
    /// Funkcja konwertująca obraz formatu tiff z png
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca obraz tiff</param>
    /// <returns>Zwraca tablicę bajtową zawierającą obraz w formacie png</returns>
    /// <exception cref="Exception"></exception>
    public static byte[] ConvertTiffToPng(byte[] tiffBytes)
    {
        string memPath = $"/vsimem/img_convert_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Błąd odczytu TIFF przez GDAL");

            int w = ds.RasterXSize;
            int h = ds.RasterYSize;

            using var bandBlue = ds.GetRasterBand(1);
            using var bandGreen = ds.GetRasterBand(2);
            using var bandRed = ds.GetRasterBand(3);

            int[] rBuffer = new int[w * h];
            int[] gBuffer = new int[w * h];
            int[] bBuffer = new int[w * h];

            bandRed.ReadRaster(0, 0, w, h, rBuffer, w, h, 0, 0);
            bandGreen.ReadRaster(0, 0, w, h, gBuffer, w, h, 0, 0);
            bandBlue.ReadRaster(0, 0, w, h, bBuffer, w, h, 0, 0);

            using var image = new Image<Rgb24>(w, h);

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < h; y++)
                {
                    var pixelRow = accessor.GetRowSpan(y);
                    int rowOffset = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        int i = rowOffset + x;
                        byte r = (byte)Math.Min((rBuffer[i] * 255f) / 16383f, 255);
                        byte g = (byte)Math.Min((gBuffer[i] * 255f) / 16383f, 255);
                        byte b = (byte)Math.Min((bBuffer[i] * 255f) / 16383f, 255);
                        pixelRow[x] = new Rgb24(r, g, b);
                    }
                }
            });

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    /// <summary>
    /// Tworzy nakładkę dla macierzy klas wielowskaźnikowych.
    /// Klasy: 0-dobry, 1-zadowalający, 2-zagrożenie NDVI, 3-zagrożenie GNDVI, 4-zagrożenie NDWI, 5-zagrożenie łączone.
    /// </summary>
    /// <param name="classMatrix">Macierz klas pikseli.</param>
    /// <returns>Tablica bajtowa PNG z przezroczystą nakładką.</returns>
    public static byte[] CreateMultiIndexOverlay(int[][] classMatrix)
    {
        int height = classMatrix.Length;
        if (height == 0)
            return Array.Empty<byte>();

        int width = classMatrix[0].Length;
        using var image = new Image<Rgba32>(width, height);
        image.Mutate(ctx => ctx.Fill(new Rgba32(0, 0, 0, 0)));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Rgba32 color = classMatrix[y][x] switch
                {
                    0 => new Rgba32(0, 255, 0, 110),       // dobry - zielony
                    1 => new Rgba32(255, 255, 0, 110),     // zadowalający - żółty
                    2 => new Rgba32(139, 69, 19, 140),     // zagrożenie NDVI - brązowy
                    3 => new Rgba32(255, 0, 255, 140),     // zagrożenie GNDVI - magenta
                    4 => new Rgba32(0, 162, 255, 140),     // zagrożenie NDWI - #00A2FF
                    5 => new Rgba32(255, 0, 0, 150),       // zagrożenie NDWI + GNDVI - czerwony
                    _ => new Rgba32(0, 0, 0, 0)
                };

                if (color.A > 0)
                    image[x, y] = color;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Funkcja rysująca obrys pola na zdjęciu png
    /// </summary>
    /// <param name="imageBytes">Tablica bajtowa zawierająca obraz png</param>
    /// <param name="geoJson">Geojson zawierający infomracje po polygonie pola</param>
    /// <param name="bbox">Współrzędne zdjęcia w przestrzeni</param>
    /// <param name="isThick">Zmienna boolowska mówiąca czy obraz ma być grubszy (obraz ndvi)</param>
    /// <returns>Tablica bajtowa z obrazem png i naniesionym na nim zarysie pola</returns>
    public static byte[] DrawGeoJsonPolygonOnImage(byte[] imageBytes, string geoJson, Bbox? bbox, bool isThick)
    {
        Console.WriteLine(geoJson);
        Console.WriteLine(bbox);
        using var image = Image.Load(imageBytes);

        if (bbox == null)
        {
            Console.WriteLine("DEBUG: Bbox jest NULL! Nie mogę rysować.");
        }
        else
        {
            var points = GeoUtils.GetPolygonPixels(geoJson, bbox, image.Width, image.Height);
            Console.WriteLine($"DEBUG: Znaleziono {points.Count} punktów do narysowania.");

            if (points.Count > 2)
            {
                float thickness = isThick ? 2.5f : 0.5f;

                Console.WriteLine($"DEBUG: Wymiary obrazka: {image.Width}x{image.Height}");
                foreach (var p in points)
                {
                    Console.WriteLine($"DEBUG PIXEL: X={p.X}, Y={p.Y}");
                }

                image.Mutate(ctx => ctx.DrawPolygon(Color.Red, thickness, points.ToArray()));
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
    /// <summary>
    /// Funkcja nanosząca na zdjęcie nakładkę (wynik klastrowania)
    /// </summary>
    /// <param name="baseBytes">Bazowe zdjęcie</param>
    /// <param name="overlayBytes">Tablica bajtowa zawierająca nakładkę</param>
    /// <returns>Połączone zdjęcie w tablicy bajtowej</returns>
    public static byte[] CombineImages(byte[] baseBytes, byte[] overlayBytes)
    {
        using var baseImg = Image.Load<Rgba32>(baseBytes);
        using var overlay = Image.Load<Rgba32>(overlayBytes);

        baseImg.Mutate(ctx => ctx.DrawImage(overlay, 1f));

        using var ms = new MemoryStream();
        baseImg.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Funkcja tworząca nakładkę na wizualizację NDVI, wykorzystując dane z klastrowania
    /// </summary>
    /// <param name="points">Współrzędne punktów leżacych w granicy pola</param>
    /// <param name="labels">Etykiety klas</param>
    /// <param name="width">Szerokość wizualizacji</param>
    /// <param name="height">Wysokość wizualizacji</param>
    /// <param name="means">Średnie w klastrach zagrożonych</param>
    /// <param name="maxBadNdvi">Maksymalna wartość NDVI "zagrażającego"</param>
    /// <returns>Tablica bajtów zawierająca nakładkę w postaci png</returns>
    public static byte[] CreateRiskOverlayFromPoints(double[][] points, int[] labels, int width, int height, Dictionary<string, double> means, double maxBadNdvi)
    {
        int scale = 6;
        int newW = width * scale;
        int newH = height * scale;

        using var image = new Image<Rgba32>(newW, newH);
        image.Mutate(ctx => ctx.Fill(new Rgba32(0, 0, 0, 0)));

        for (int i = 0; i < points.Length; i++)
        {
            int x = (int)points[i][0];
            int y = (int)points[i][1];
            int label = labels[i];

            Rgba32 color;
            if (label == 0) color = HealthyOverlayColor;
            else if (label == 1) color = WarningOverlayColor;
            else {
                string key = label.ToString();
                double clusterMean = means.TryGetValue(key, out double value) ? value : 0.0;

                color = GetColorBySeverity(clusterMean, maxBadNdvi);
            }

            if (color.A > 0)
            {
                for (int dy = 0; dy < scale; dy++)
                    for (int dx = 0; dx < scale; dx++)
                        image[x * scale + dx, y * scale + dy] = color;
            }
        }

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }
    /// <summary>
    /// Funkcja tworząca legendę nakładki z danymi klasteryzacji
    /// </summary>
    /// <param name="means">Średnie wartości NDVI w klastrach</param>
    /// <param name="presentClusters">Lista klastrów</param>
    /// <param name="maxBadNdvi">Maksymalna wartość NDVI "zagrażającego"</param>
    /// <param name="darkMode">Czy ustawiony darkmode (potrzebne do zmiany kolorów)</param>
    /// <returns>Tablica bajtowa z legendą w formacie png</returns>
    public static byte[] CreateLegendWithClusters(Dictionary<string, double> means, int[] presentClusters, double maxBadNdvi, bool darkMode)
    {
        var riskClusters = presentClusters.Distinct().Where(id => id < 0).ToList();

        int h = 170 + (riskClusters.Count * 30);
        int w = 450;

        using var image = new Image<Rgba32>(w, h);

        Color bgColor = darkMode ? Color.ParseHex("#2b2b2b") : Color.White;
        Color textColor = darkMode ? Color.White : Color.Black;

        image.Mutate(ctx => ctx.Fill(bgColor));

        var font = SystemFonts.CreateFont("Arial", 12);
        var titleFont = SystemFonts.CreateFont("Arial", 14, FontStyle.Bold);
        int y = 20;

        image.Mutate(ctx => {
            ctx.DrawText("Legenda Ryzyka", titleFont, textColor, new PointF(10, y));
            y += 35;

            DrawLegendItem(ctx, new Rgba32(HealthyOverlayColor.R, HealthyOverlayColor.G, HealthyOverlayColor.B, 255), "Zdrowe (Wysokie NDVI)", ref y, font, textColor);
            DrawLegendItem(ctx, new Rgba32(WarningOverlayColor.R, WarningOverlayColor.G, WarningOverlayColor.B, 255), "Ostrzegawcze (Średnie)", ref y, font, textColor);

            if (riskClusters.Count > 0)
            {
                y += 10;
                ctx.DrawText("Zidentyfikowane Ogniska:", SystemFonts.CreateFont("Arial", 12, FontStyle.Bold), textColor, new PointF(10, y));
                y += 25;

                foreach (var clusterId in riskClusters.OrderByDescending(id => id))
                {
                    string key = clusterId.ToString();
                    double val = means.ContainsKey(key) ? means[key] : 0.0;

                    Rgba32 color = GetColorBySeverity(val, maxBadNdvi);

                    string label = $"Ognisko #{Math.Abs(clusterId)} (Średnia: {val:F2})";
                    DrawLegendItem(ctx, color, label, ref y, font, textColor);
                }
            }
        });

        using var ms = new MemoryStream();
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Funkcja konwertująca macierz typu double na listę list typu double
    /// </summary>
    /// <param name="array">Macierz do przekonwertowania</param>
    /// <returns>Lista list z danymi macierzy</returns>
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
    /// <summary>
    /// Funkcja konwertująca listę list double na macierz typu double
    /// </summary>
    /// <param name="list">Lista list do przekonwertowania</param>
    /// <returns>Macierz z danymi listy list</returns>
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

    #endregion

    #region Private Methods
    /// <summary>
    /// Pomocnicza funkcja do rysowania elementów legendy
    /// </summary>
    /// <param name="ctx">Obiekt (biblioteka SixLabours) wykonujący akcje rysowania legendy</param>
    /// <param name="boxColor">Kolor przyporządkowany danemu rekordowi</param>
    /// <param name="text">Tekst dla danego rekordu</param>
    /// <param name="y">Aktualna wysokość legendy (z każdym rekordem się zwiększa)</param>
    /// <param name="font">Czcionka</param>
    /// <param name="textColor">Kolor tekstu</param>
    private static void DrawLegendItem(IImageProcessingContext ctx, Color boxColor, string text, ref int y, Font font, Color textColor)
    {
        ctx.Fill(boxColor, new RectangleF(20, y, 20, 20));
        ctx.DrawText(text, font, textColor, new PointF(50, y + 2));
        y += 30;
    }
    /// <summary>
    /// Funkcja pomocnicza generująca kolor na podstawie średniego NDVI klastra
    /// </summary>
    /// <param name="meanNdvi">Średnie NDVI klastra</param>
    /// <param name="maxThreshold">Maksymalne alarmujące NDVI</param>
    /// <returns>Kolor w formacie RGB</returns>
    /// <exception cref="ArgumentException"></exception>
    private static Rgba32 GetColorBySeverity(double meanNdvi, double maxThreshold)
{
    if (maxThreshold <= 0.0001)
    {
        throw new ArgumentException(
            "Parametr 'maxBadNdvi' jest wymagany i musi być większy od 0. Sprawdź konfigurację algorytmu.",
            nameof(maxThreshold)
        );
    }

    double t = Math.Clamp(meanNdvi / maxThreshold, 0.0, 1.0);

    byte r = (byte)(255 * t);
    byte g = (byte)(42 * t);
    byte b = (byte)(46 * t);

    return new Rgba32(r, g, b, 200);
}

    #endregion
}