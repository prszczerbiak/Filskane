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
    public static byte[] RenderVegetationHeatmap(ReadOnlySpan<float> vegetationArray, int width, int height)
    {
        double[,] vegetationMatrix = ToHeatmapMatrix(vegetationArray, width, height);

        ScottPlot.Plot plot = new();

        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(vegetationMatrix);

        hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();

        hm.ManualRange = new ScottPlot.Range(0, 1);

        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        return plot.GetImageBytes(width * 6, height * 6, ImageFormat.Png);
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