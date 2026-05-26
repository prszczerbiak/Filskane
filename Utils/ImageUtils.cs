using OSGeo.GDAL;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Filskane.Models;
using MaxRev.Gdal.Core;
using System.Buffers;
using System.Text.Json;

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
    public static unsafe byte[] ConvertTiffToPng(byte[] tiffBytes)
    {
        string memPath = $"/vsimem/img_convert_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Błąd odczytu TIFF przez GDAL");

            int w = ds.RasterXSize;
            int h = ds.RasterYSize;
            int size = w * h;

            var bandBlue = ds.GetRasterBand(1);
            var bandGreen = ds.GetRasterBand(2);
            var bandRed = ds.GetRasterBand(3);


            int[] rBuffer = ArrayPool<int>.Shared.Rent(size);
            int[] gBuffer = ArrayPool<int>.Shared.Rent(size);
            int[] bBuffer = ArrayPool<int>.Shared.Rent(size);

            try
            {
                fixed (int* rPtr = rBuffer, gPtr = gBuffer, bPtr = bBuffer)
                {
                    bandRed.ReadRaster(0, 0, w, h, (IntPtr)rPtr, w, h, DataType.GDT_Int32, 0, 0);
                    bandGreen.ReadRaster(0, 0, w, h, (IntPtr)gPtr, w, h, DataType.GDT_Int32, 0, 0);
                    bandBlue.ReadRaster(0, 0, w, h, (IntPtr)bPtr, w, h, DataType.GDT_Int32, 0, 0);


                    byte* lut = stackalloc byte[16384];

                    nint lutPointer = (nint)lut;
                    nint rPointer = (nint)rPtr;
                    nint gPointer = (nint)gPtr;
                    nint bPointer = (nint)bPtr;

                    for (int i = 0; i < 16384; i++)
                    {
                        lut[i] = (byte)Math.Min((i * 255f) / 16383f, 255);
                    }

                    using var image = new Image<Rgb24>(w, h);

                    image.ProcessPixelRows(accessor =>
                    {
                        byte* localLut = (byte*)lutPointer;
                        int* localR = (int*)rPointer;
                        int* localG = (int*)gPointer;
                        int* localB = (int*)bPointer;

                        for (int y = 0; y < h; y++)
                        {
                            var pixelRow = accessor.GetRowSpan(y);
                            int rowOffset = y * w;
                            int* currentR = localR + rowOffset;
                            int* currentG = localG + rowOffset;
                            int* currentB = localB + rowOffset;

                            fixed (Rgb24* destRow = pixelRow)
                            {
                                Rgb24* dest = destRow;

                                for (int x = 0; x < w; x++)
                                {
                                    int clampR = Math.Clamp(*currentR, 0, 16383);
                                    int clampG = Math.Clamp(*currentG, 0, 16383);
                                    int clampB = Math.Clamp(*currentB, 0, 16383);

                                    dest->R = localLut[clampR];
                                    dest->G = localLut[clampG];
                                    dest->B = localLut[clampB];

                                    currentR++;
                                    currentG++;
                                    currentB++;
                                    dest++;
                                }
                            }
                        }
                    });

                    using var ms = new MemoryStream(size * 3);
                    image.SaveAsPng(ms);
                    return ms.ToArray();
                }
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rBuffer);
                ArrayPool<int>.Shared.Return(gBuffer);
                ArrayPool<int>.Shared.Return(bBuffer);
            }
            
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
    public static unsafe byte[] CreateMultiIndexOverlay(int[][] classMatrix)
    {
        int height = classMatrix.Length;
        if (height == 0)
            return Array.Empty<byte>();

        int width = classMatrix[0].Length;
        using var image = new Image<Rgba32>(width, height);

        
        image.ProcessPixelRows(accessor =>
        {
            Rgba32* colorLut = stackalloc Rgba32[7]
            {
                new Rgba32(0, 255, 0, 110),       // dobry - zielony
                new Rgba32(255, 255, 0, 110),     // zadowalający - żółty
                new Rgba32(139, 69, 19, 140),     // zagrożenie NDVI - brązowy
                new Rgba32(255, 0, 255, 140),     // zagrożenie GNDVI - magenta
                new Rgba32(0, 162, 255, 140),     // zagrożenie NDWI - #00A2FF
                new Rgba32(255, 0, 0, 150),        // zagrożenie NDWI + GNDVI - czerwony
                new Rgba32(0, 0, 0, 0)
            };
            for (int y = 0; y < height; y++)
            {
                Span<Rgba32> pixelRow = accessor.GetRowSpan(y);
                int[] matrixRow = classMatrix[y];

                fixed (int* srcPtr = matrixRow)
                fixed(Rgba32* dstPtr = pixelRow)
                {
                    int* src = srcPtr;
                    Rgba32* dst = dstPtr;
                    for (int x = 0; x < width; x++)
                    {
                        uint val = (uint)(*src);
                        int safeIndex = val < 6u ? (int)val : 6;
                        *dst = colorLut[safeIndex];
                        src++;
                        dst++;
                    }
                }
            }
        });
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
    public static byte[] DrawGeoJsonPolygonOnImage(byte[] imageBytes, JsonElement geoJson, Bbox? bbox, bool isThick)
    {
        if (bbox == null)
            return imageBytes;

        int width, height;

        {
            var imageInfo = Image.Identify(imageBytes);
            width = imageInfo.Width;
            height = imageInfo.Height;

            if (imageInfo is IDisposable disposableInfo)
            {
                disposableInfo.Dispose();
            }
        }
        
        var points = GeoUtils.GetPolygonPixels(geoJson, bbox, width, height);

        if (points == null || points.Count() <= 2)
            return imageBytes;

        using var image = Image.Load(imageBytes);

        float thickness = isThick ? 2.5f : 0.5f;

        image.Mutate(ctx => ctx.DrawPolygon(Color.Red, thickness, points.ToArray()));
        
        using var ms = new MemoryStream(imageBytes.Length);
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

        if(overlayBytes == null || overlayBytes.Length == 0)
            return baseBytes;

        if(baseBytes == null || baseBytes.Length == 0)
            return overlayBytes;

        using var baseImg = Image.Load<Rgba32>(baseBytes);
        using var overlay = Image.Load<Rgba32>(overlayBytes);

        baseImg.Mutate(ctx => ctx.DrawImage(overlay, 1f));

        using var ms = new MemoryStream(baseBytes.Length);
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

        Dictionary<int, Rgba32> colorCache = new Dictionary<int, Rgba32>();
        colorCache[0] = HealthyOverlayColor;
        colorCache[1] = WarningOverlayColor;

        if(means != null){
            foreach(var kvp in means)
            {
                if(int.TryParse(kvp.Key, out int clusterId) && clusterId < 0)
                {
                    colorCache[clusterId] = GetColorBySeverity(kvp.Value, maxBadNdvi);
                }
            }
        }
        
        image.ProcessPixelRows(accessor =>
        {
            for (int i = 0; i < points.Length; i++)
            {
                double[] pt = points[i];
                int x = (int)pt[0];
                int y = (int)pt[1];
                int label = labels[i];

                if (colorCache.TryGetValue(label, out Rgba32 color) && color.A > 0)
                {
                    int startX = x * scale;
                    int startY = y * scale;
                    for (int dy = 0; dy < scale; dy++)
                    {
                        Span<Rgba32> pixelRow = accessor.GetRowSpan(startY + dy);

                        pixelRow.Slice(startX, scale).Fill(color);
                        
                    }
                }
            }
        });
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
        var riskClusters = presentClusters.Distinct().Where(id => id < 0).OrderByDescending(id => id).ToList();

        int h = 170 + (riskClusters.Count * 30);
        int w = 450;

        using var image = new Image<Rgba32>(w, h);

        Color bgColor = darkMode ? Color.ParseHex("#2b2b2b") : Color.White;
        Color textColor = darkMode ? Color.White : Color.Black;

        var font = SystemFonts.CreateFont("Arial", 12);
        var headerFont = SystemFonts.CreateFont("Arial", 12, FontStyle.Bold);
        var titleFont = SystemFonts.CreateFont("Arial", 14, FontStyle.Bold);
        int y = 20;

        image.Mutate(ctx => {
            ctx.Fill(bgColor);

            ctx.DrawText("Legenda Ryzyka", titleFont, textColor, new PointF(10, y));
            y += 35;

            DrawLegendItem(ctx, new Rgba32(HealthyOverlayColor.R, HealthyOverlayColor.G, HealthyOverlayColor.B, 255), "Zdrowe (Wysokie NDVI)", ref y, font, textColor);
            DrawLegendItem(ctx, new Rgba32(WarningOverlayColor.R, WarningOverlayColor.G, WarningOverlayColor.B, 255), "Ostrzegawcze (Średnie)", ref y, font, textColor);

            if (riskClusters.Count > 0)
            {
                y += 10;
                ctx.DrawText("Zidentyfikowane Ogniska:", headerFont, textColor, new PointF(10, y));
                y += 25;

                foreach (var clusterId in riskClusters)
                {
                    string key = clusterId.ToString();
                    means.TryGetValue(key, out double val);

                    Rgba32 color = GetColorBySeverity(val, maxBadNdvi);

                    string label = $"Ognisko #{Math.Abs(clusterId)} (Średnia: {val:F2})";
                    DrawLegendItem(ctx, color, label, ref y, font, textColor);
                }
            }
        });

        using var ms = new MemoryStream(32 * 1024);
        image.SaveAsPng(ms);
        return ms.ToArray();
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
        y += 2;
        ctx.DrawText(text, font, textColor, new PointF(50, y));
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