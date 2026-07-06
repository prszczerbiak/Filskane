using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Filskane.Utils;
/// <summary>
/// Klasa pomocnicza korzystająca z narzędzi do rysowania wykresów
/// </summary>
public static class PlotUtils
{
    #region Public Methods
    /// <summary>
    /// Funkcja rysująca mapę ciepła indeksów wegetacji (NDVI, GNDVI, SAVI, EVI)
    /// </summary>
    /// <param name="vegetationMatrix"></param>
    /// <returns>Tablica bajtowa zawierająca mapę ciepła w formacie png</returns>
    public static unsafe byte[] RenderVegetationHeatmap(ReadOnlySpan<float> vegetationArray, int width, int height)
    {
        const int scale = 6;
        int outputWidth = width * scale;
        int outputHeight = height * scale;

        using var image = new Image<Rgba32>(outputWidth, outputHeight);

        if (image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMemory))
        {
            fixed (float* ptr = vegetationArray)
            {
                nint address = (nint)ptr;
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
                };

                // Lambda jest teraz w 100% bezpieczna kompilacyjnie
                Parallel.For(0, outputHeight, parallelOptions, y =>
                {
                    float* vegetationPtr = (float*)address;

                    // 2. Wewnątrz wątku bierzemy całą pamięć obrazka, robimy z niej Spana, 
                    // i wycinamy (Slice) tylko ten jeden, unikalny dla tego wątku wiersz!
                    Span<Rgba32> pixelRow = imageMemory.Span.Slice(y * outputWidth, outputWidth);

                    int inputY = y / scale;

                    for (int x = 0; x < outputWidth; x++)
                    {
                        int inputX = x / scale;
                        int dataIndex = (inputY * width) + inputX;

                        float value = vegetationPtr[dataIndex];
                        byte greenComponent = (byte)(255 - (value * 155));
                        byte redBlueComponent = (byte)((1 - value) * 240);

                        pixelRow[x] = new Rgba32(redBlueComponent, greenComponent, redBlueComponent, 255);
                    }
                });
            }
        }

        int maxEstimatedSize = outputWidth * outputHeight * 4;
        using var ms = new MemoryStream(maxEstimatedSize);
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    public static byte[] RenderNDWIHeatmap(ReadOnlySpan<float> ndwiArray, int width, int height)
    {
        double[,] ndwiMatrix = ToHeatmapMatrix(ndwiArray, width, height);

        ScottPlot.Plot plot = new();

        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(ndwiMatrix);

        hm.Colormap = new ScottPlot.Colormaps.Blues().Reversed();

        hm.ManualRange = new ScottPlot.Range(0, 1);

        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        return plot.GetImageBytes(width * 6, height * 6, ImageFormat.Png);
    }
    #endregion

    #region Private Methods
    private static double[,] ToHeatmapMatrix(ReadOnlySpan<float> values, int width, int height)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), "Wymiary macierzy muszą być większe od zera.");

        if (values.Length != width * height)
            throw new ArgumentException("Długość danych nie odpowiada podanym wymiarom macierzy.");

        double[,] matrix = new double[height, width];
        for (int y = 0; y < height; y++)
        {
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                float value = values[rowOffset + x];
                matrix[y, x] = float.IsFinite(value) ? value : 0d;
            }
        }

        return matrix;
    }

    #endregion
}