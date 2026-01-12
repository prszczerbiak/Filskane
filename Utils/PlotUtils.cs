using ScottPlot;

namespace Filskane.Utils;
/// <summary>
/// Klasa pomocnicza korzystająca z narzędzi do rysowania wykresów
/// </summary>
public static class PlotUtils
{
    #region Public Methods
    /// <summary>
    /// Funkcja rysująca mapę ciepła NDVI
    /// </summary>
    /// <param name="ndviMatrix"></param>
    /// <returns>Tablica bajtowa zawierająca mapę ciepła w formacie png</returns>
    public static byte[] RenderNdviHeatmap(double[,] ndviMatrix)
    {
        ScottPlot.Plot plot = new();

        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(ndviMatrix);

        hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();

        hm.ManualRange = new ScottPlot.Range(0, 1);

        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        int width = ndviMatrix.GetLength(1) * 6;
        int height = ndviMatrix.GetLength(0) * 6;

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
    #endregion
}