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
    public static byte[] RenderVegetationHeatmap(double[,] vegetationMatrix)
    {
        ScottPlot.Plot plot = new();

        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(vegetationMatrix);

        hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();

        hm.ManualRange = new ScottPlot.Range(0, 1);

        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        int width = vegetationMatrix.GetLength(1) * 6;
        int height = vegetationMatrix.GetLength(0) * 6;

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }

    public static byte[] RenderNDWIHeatmap(double[,] ndwiMatrix)
    {
        ScottPlot.Plot plot = new();

        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(ndwiMatrix);

        hm.Colormap = new ScottPlot.Colormaps.Blues().Reversed();

        hm.ManualRange = new ScottPlot.Range(0, 1);

        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        int width = ndwiMatrix.GetLength(1) * 6;
        int height = ndwiMatrix.GetLength(0) * 6;

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
    #endregion
}