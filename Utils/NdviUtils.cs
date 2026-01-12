using OSGeo.GDAL;
using System;

namespace WebApplication1.Utils;
/// <summary>
/// Klasa pomocnicza dotycząca NDVI
/// </summary>
public static class NdviUtils
{
    #region Public Methods
    /// <summary>
    /// Funkcja przeliczająca NDVI
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca NDVI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static double[,] CalculateNdvi(byte[] tiffBytes)
    {
        Gdal.AllRegister();

        string memPath = $"/vsimem/ndvi_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);

            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var bandRed = ds.GetRasterBand(3);
            using var bandNir = ds.GetRasterBand(4);

            int[] redBuffer = new int[width * height];
            int[] nirBuffer = new int[width * height];

            bandRed.ReadRaster(0, 0, width, height, redBuffer, width, height, 0, 0);
            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);

            double[,] ndvi = new double[height, width];

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double red = redBuffer[i];
                double nir = nirBuffer[i];

                if (nir + red == 0)
                {
                    ndvi[y, x] = 0;
                }
                else
                {
                    double val = (nir - red) / (nir + red);
                    ndvi[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return ndvi;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }
    #endregion
}
