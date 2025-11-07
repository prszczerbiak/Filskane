using BitMiracle.LibTiff.Classic;
using ScottPlot;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Text.Json;



namespace WebApplication1.Models
{
    public class ScanResult
    {
        public DateTime ScanDate { get; set; }
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();

        public string? Bbox { get; set; }

        public string? FieldBbox { get; set; }

        public static byte[] ConvertTiffBytesToRgbPng(byte[] tiffBytes)
        {
            using var inputStream = new MemoryStream(tiffBytes);
            using var tiff = Tiff.ClientOpen("in-memory", "r", inputStream, new TiffStream());

            if (tiff == null)
                throw new Exception("Nie można odczytać danych TIFF.");

            int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();

            if (samplesPerPixel < 3 || bitsPerSample != 16)
                throw new Exception("Oczekiwano 4 pasm 16-bitowych w przeplocie BIP.");

            int scanlineSize = tiff.ScanlineSize();
            byte[] scanline = new byte[scanlineSize];

            using var image = new Image<Rgb24>(width, height);

            ushort[] channelMax = [0, 0, 0];

            //ushort max = 0;

            for (int y = 0; y < height; y++)
            {
                tiff.ReadScanline(scanline, y);
                for (int x = 0; x < width; x++)
                {
                    int offset = x * samplesPerPixel * 2;

                    for (int c = 0; c < 3; c++)
                    {
                        ushort value = BitConverter.ToUInt16(scanline, offset + c * 2);
                        if (value > channelMax[c]) channelMax[c] = value;
                        //if(value > max) max = value;
                    }
                }
            }

            for (int y = 0; y < height; y++)
            {

                tiff.ReadScanline(scanline, y);
                for (int x = 0; x < width; x++)
                {


                    int offset = x * samplesPerPixel * 2; // 2 bajty na pasmo (16 bitów)
                    ushort b02 = BitConverter.ToUInt16(scanline, offset + 0);
                    ushort b03 = BitConverter.ToUInt16(scanline, offset + 2);
                    ushort b04 = BitConverter.ToUInt16(scanline, offset + 4);

                    // ushort b08 = BitConverter.ToUInt16(scanline, offset + 6); // NIR — ignorujemy

                    // skalowanie 16-bit → 8-bit
                    byte r = (byte)(Math.Min(b04 * 255f / 16383, 255));
                    byte g = (byte)(Math.Min(b03 * 255f / 16383, 255));
                    byte b = (byte)(Math.Min(b02 * 255f / 16383, 255));

                    //byte r = (byte)(Math.Min(b04 * 255f / 10000, 255));
                    //byte g = (byte)(Math.Min(b03 * 255f / 10000, 255));
                    //byte b = (byte)(Math.Min(b02 * 255f / 10000, 255));

                    //byte r = (byte)(Math.Min(b04 * 255f / max, 255));
                    //byte g = (byte)(Math.Min(b03 * 255f / max, 255));
                    //byte b = (byte)(Math.Min(b02 * 255f / max, 255));

                    image[x, y] = new Rgb24(r, g, b);
                }
            }

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        //public static byte[] ConvertTiffToNdviHeatmap(byte[] tiffBytes)
        //{
        //    using var inputStream = new MemoryStream(tiffBytes);
        //    using var tiff = Tiff.ClientOpen("in-memory", "r", inputStream, new TiffStream());
        //    if (tiff == null)
        //        throw new Exception("Nie można odczytać danych TIFF.");

        //    int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        //    int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        //    Console.WriteLine(width + "x" + height);
        //    int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
        //    int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();

        //    if (samplesPerPixel < 4 || bitsPerSample != 16)
        //        throw new Exception("Oczekiwano 4 pasm 16-bitowych w przeplocie BIP (B02,B03,B04,B08).");

        //    int scanlineSize = tiff.ScanlineSize();
        //    byte[] scanline = new byte[scanlineSize];

        //    // Przygotuj tablicę NDVI
        //    double[,] ndvi = new double[height, width];

        //    for (int y = 0; y < height; y++)
        //    {
        //        tiff.ReadScanline(scanline, y);
        //        for (int x = 0; x < width; x++)
        //        {
        //            int offset = x * samplesPerPixel * 2;

        //            ushort b04 = BitConverter.ToUInt16(scanline, offset + 4); // RED
        //            ushort b08 = BitConverter.ToUInt16(scanline, offset + 6); // NIR

        //            float nir = b08 / 16383f;
        //            float red = b04 / 16383f;

        //            float ndviValue = (nir + red == 0) ? 0 : (nir - red) / (nir + red);
        //            ndviValue = Math.Clamp(ndviValue, -1f, 1f);

        //            // Uwaga: w heatmapie (0,0) to dół, a w TIFF góra
        //            ndvi[y, x] = ndviValue;
        //        }
        //    }

        //    // Generowanie heatmapy
        //    ScottPlot.Plot plot = new();
        //    PixelPadding padding = new(-22, -22, -34, -34);
        //    plot.Layout.Fixed(padding);
        //    //plot.Layout.Fixed(new ScottPlot.PixelPadding(left: 0, right: 100, top: 0, bottom: 0));
        //    var hm = plot.Add.Heatmap(ndvi); // Turbo -> dobra paleta od niebieskiego do zielonego
        //    plot.Grid.IsVisible = false;
        //    plot.Axes.Bottom.IsVisible = false;
        //    plot.Axes.Left.IsVisible = false;
        //    plot.Axes.Right.IsVisible = false;
        //    plot.Axes.Top.IsVisible = false;
        //    hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();
        //    hm.ManualRange = new ScottPlot.Range(0, 1);
        //    ScottPlot.Plot hlp = new();
        //    hlp.Layout.Fixed(new ScottPlot.PixelPadding(left: 5, right: 5, top: 0, bottom: 500));
        //    var cb = hlp.Add.ColorBar(hm, Edge.Bottom);

        //    hlp.Grid.IsVisible = false;
        //    hlp.Axes.Bottom.IsVisible = false;
        //    hlp.Axes.Left.IsVisible = false;
        //    hlp.Axes.Right.IsVisible = false;
        //    hlp.Axes.Top.IsVisible = false;
        //    cb.Width = 30;

        //    hlp.SavePng("colorbar.png", (width) * 8, 100);
        //    return plot.GetImageBytes((width) * 6, (height) * 6, ScottPlot.ImageFormat.Png);

        //}

        public static double[,] CalculateNdvi(byte[] tiffBytes)
        {
            using var inputStream = new MemoryStream(tiffBytes);
            using var tiff = Tiff.ClientOpen("in-memory", "r", inputStream, new TiffStream());
            if (tiff == null)
                throw new Exception("Nie można odczytać danych TIFF.");

            int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            int bitsPerSample = tiff.GetField(TiffTag.BITSPERSAMPLE)[0].ToInt();

            if (samplesPerPixel < 4 || bitsPerSample != 16)
                throw new Exception("Oczekiwano 4 pasm 16-bitowych w przeplocie BIP (B02,B03,B04,B08).");

            int scanlineSize = tiff.ScanlineSize();
            byte[] scanline = new byte[scanlineSize];

            double[,] ndvi = new double[height, width];

            for (int y = 0; y < height; y++)
            {
                tiff.ReadScanline(scanline, y);
                for (int x = 0; x < width; x++)
                {
                    int offset = x * samplesPerPixel * 2;

                    ushort b04 = BitConverter.ToUInt16(scanline, offset + 4); // RED
                    ushort b08 = BitConverter.ToUInt16(scanline, offset + 6); // NIR

                    float nir = b08 / 16383f;
                    float red = b04 / 16383f;

                    float ndviValue = (nir + red == 0) ? 0 : (nir - red) / (nir + red);
                    ndviValue = Math.Clamp(ndviValue, -1f, 1f);

                    ndvi[y, x] = ndviValue;
                }
            }

            return ndvi;
        }

        public static byte[] RenderNdviHeatmap(double[,] ndvi)
        {
            int height = ndvi.GetLength(0);
            int width = ndvi.GetLength(1);

            // Tworzenie wykresu heatmapy
            ScottPlot.Plot plot = new();
            plot.Layout.Fixed(new ScottPlot.PixelPadding(-22, -22, -34, -34));

            var hm = plot.Add.Heatmap(ndvi);
            hm.Colormap = new ScottPlot.Colormaps.Greens().Reversed();
            hm.ManualRange = new ScottPlot.Range(0, 1);

            plot.Grid.IsVisible = false;
            plot.Axes.Bottom.IsVisible = false;
            plot.Axes.Left.IsVisible = false;
            plot.Axes.Right.IsVisible = false;
            plot.Axes.Top.IsVisible = false;

            // Dodanie kolorowej skali (opcjonalnie)
            //ScottPlot.Plot hlp = new();
            //hlp.Layout.Fixed(new ScottPlot.PixelPadding(left: 5, right: 5, top: 0, bottom: 500));
            //var cb = hlp.Add.ColorBar(hm, Edge.Bottom);

            //hlp.Grid.IsVisible = false;
            //hlp.Axes.Bottom.IsVisible = false;
            //hlp.Axes.Left.IsVisible = false;
            //hlp.Axes.Right.IsVisible = false;
            //hlp.Axes.Top.IsVisible = false;
            //cb.Width = 30;

            // Zwrócenie obrazu jako PNG
            return plot.GetImageBytes(width * 6, height * 6, ScottPlot.ImageFormat.Png);
        }

    }
}
