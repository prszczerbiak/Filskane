using ScottPlot;

namespace WebApplication1.Utils;

public static class PlotUtils
{
    public static byte[] RenderNdviHeatmap(double[,] ndviMatrix)
    {
        ScottPlot.Plot plot = new();

        // Usunięcie marginesów, aby obraz był wypełniony mapą
        plot.Layout.Fixed(new PixelPadding(0, 0, 0, 0));

        var hm = plot.Add.Heatmap(ndviMatrix);

        // Kolorystyka: Odwrócona zieleń (ciemny zielony = wysokie NDVI, jasny/brązowy = niskie)
        hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();

        // Sztywne ustawienie zakresu NDVI 0..1 (wszystko poniżej 0 to woda/chmury/błąd)
        hm.ManualRange = new ScottPlot.Range(0, 1);

        // Ukrycie osi i siatki
        plot.Axes.Frameless();
        plot.Grid.IsVisible = false;
        plot.Axes.Margins(0, 0);

        // Zwiększenie rozdzielczości wyjściowej dla lepszej jakości (np. 6x rozmiar macierzy)
        int width = ndviMatrix.GetLength(1) * 6;
        int height = ndviMatrix.GetLength(0) * 6;

        return plot.GetImageBytes(width, height, ImageFormat.Png);
    }
}