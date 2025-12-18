using BitMiracle.LibTiff.Classic;

namespace WebApplication1.Utils;

public static class NdviUtils
{
    // Zwraca macierz wartości NDVI od -1.0 do 1.0
    public static double[,] CalculateNdvi(byte[] tiffBytes)
    {
        using var ms = new MemoryStream(tiffBytes);
        using var tiff = Tiff.ClientOpen("in-mem", "r", ms, new TiffStream());

        if (tiff == null) throw new Exception("Nie można odczytać danych TIFF.");

        int width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        int height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        int samplesPerPixel = tiff.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt(); // Oczekujemy 4 (B02, B03, B04, B08)

        int scanlineSize = tiff.ScanlineSize();
        byte[] scanline = new byte[scanlineSize];

        double[,] ndvi = new double[height, width];

        for (int y = 0; y < height; y++)
        {
            tiff.ReadScanline(scanline, y);
            for (int x = 0; x < width; x++)
            {
                int offset = x * samplesPerPixel * 2; // 16-bit = 2 bajty

                // Zakładamy kolejność kanałów z GDAL Merge: B02, B03, B04 (Red), B08 (NIR)
                // Offset 0: Blue, 2: Green, 4: Red, 6: NIR
                ushort redRaw = BitConverter.ToUInt16(scanline, offset + 4);
                ushort nirRaw = BitConverter.ToUInt16(scanline, offset + 6);

                float red = redRaw / 16383f; // Normalizacja do 0..1 (dla Sentinel-2 często dzieli się przez 10000, ale 16383 to max 14-bit)
                float nir = nirRaw / 16383f;

                double val = (nir + red == 0) ? 0 : (nir - red) / (nir + red);
                ndvi[y, x] = Math.Clamp(val, -1.0, 1.0);
            }
        }

        return ndvi;
    }
}