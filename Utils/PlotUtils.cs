using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ScottPlot;

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
    public static unsafe byte[] RenderVegetationHeatmap(ReadOnlySpan<float> array, int w, int h)
    {
        return RenderHeatmapCore(array, w, h, &GetVegetationColor);
    }

    public static unsafe byte[] RenderNDWIHeatmap(ReadOnlySpan<float> array, int w, int h)
    {
        return RenderHeatmapCore(array, w, h, &GetNDWIColor);
    }
    #endregion

    #region Private Methods

    private static Rgba32 GetVegetationColor(float value)
    {
        byte greenComponent = (byte)(255 - (value * 155));
        byte redBlueComponent = (byte)((1 - value) * 240);
        return new Rgba32(redBlueComponent, greenComponent, redBlueComponent, 255);
    }

    private static Rgba32 GetNDWIColor(float value)
    {
        value = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
        byte r = (byte)(255 * (1 - value));
        byte g = (byte)(255 * (1 - value));
        return new Rgba32(r, g, 255, 255);
    }

    // 2. Nasz uniwersalny silnik przyjmujący wskaźnik na funkcję (delegate*<float, Rgba32>)
    private static unsafe byte[] RenderHeatmapCore(ReadOnlySpan<float> dataArray, int width, int height, delegate*<float, Rgba32> colorCalculator)
    {
        const int scale = 6;
        int outputWidth = width * scale;
        int outputHeight = height * scale;

        using var image = new Image<Rgba32>(outputWidth, outputHeight);

        if (image.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> imageMemory))
        {
            fixed (float* ptr = dataArray)
            {
                nint address = (nint)ptr;
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
                };

                Parallel.For(0, height, parallelOptions, inputY =>
                {
                    float* dataPtr = (float*)address;
                    int rowOffset = inputY * width;
                    int outputYStart = inputY * scale;
                    int baseRowIndex = outputYStart * outputWidth;

                    for (int inputX = 0; inputX < width; inputX++)
                    {
                        float value = dataPtr[rowOffset + inputX];

                        // WYWOŁANIE PRZEZ WSKAŹNIK - Błyskawiczne i bez alokacji!
                        Rgba32 color = colorCalculator(value);

                        int outputXStart = inputX * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            Span<Rgba32> pixelRow = imageMemory.Span.Slice(baseRowIndex + (sy * outputWidth), outputWidth);
                            pixelRow.Slice(outputXStart, scale).Fill(color);
                        }
                    }
                });
            }
        }

        using var ms = new MemoryStream(width * height);
        image.SaveAsPng(ms);
        return ms.ToArray();
    }

    #endregion
}