using OSGeo.GDAL;
using System;

namespace Filskane.Utils;
/// <summary>
/// Klasa pomocnicza dotycząca NDVI
/// </summary>
public static class NdviUtils
{
    #region Public Methods
    /// <summary>
    /// Funkcja przeliczająca NDVI (Normalized Difference Vegetation Index)
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

            using var bandRed = ds.GetRasterBand(3); // Kanał 3: B04 (Red)
            using var bandNir = ds.GetRasterBand(7); // Kanał 7: B08 (NIR)

            int[] redBuffer = new int[width * height];
            int[] nirBuffer = new int[width * height];

            bandRed.ReadRaster(0, 0, width, height, redBuffer, width, height, 0, 0);
            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);

            double[,] result = new double[height, width];

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double red = redBuffer[i];
                double nir = nirBuffer[i];

                if (nir + red == 0)
                {
                    result[y, x] = 0;
                }
                else
                {
                    double val = (nir - red) / (nir + red);
                    result[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return result;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    /// <summary>
    /// Funkcja przeliczająca GNDVI (Green Normalized Difference Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca GNDVI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static double[,] CalculateGndvi(byte[] tiffBytes)
    {
        Gdal.AllRegister();
        string memPath = $"/vsimem/gndvi_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var bandGreen = ds.GetRasterBand(2); // Kanał 2: B03 (Green)
            using var bandNir = ds.GetRasterBand(7);   // Kanał 7: B08 (NIR)

            int[] greenBuffer = new int[width * height];
            int[] nirBuffer = new int[width * height];

            bandGreen.ReadRaster(0, 0, width, height, greenBuffer, width, height, 0, 0);
            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);

            double[,] result = new double[height, width];

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double green = greenBuffer[i];
                double nir = nirBuffer[i];

                if (nir + green == 0)
                {
                    result[y, x] = 0;
                }
                else
                {
                    double val = (nir - green) / (nir + green);
                    result[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return result;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    /// <summary>
    /// Funkcja przeliczająca SAVI (Soil Adjusted Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca SAVI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static double[,] CalculateSavi(byte[] tiffBytes)
    {
        Gdal.AllRegister();
        string memPath = $"/vsimem/savi_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var bandRed = ds.GetRasterBand(3); // Kanał 3: B04 (Red)
            using var bandNir = ds.GetRasterBand(7); // Kanał 7: B08 (NIR)

            int[] redBuffer = new int[width * height];
            int[] nirBuffer = new int[width * height];

            bandRed.ReadRaster(0, 0, width, height, redBuffer, width, height, 0, 0);
            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);

            double[,] result = new double[height, width];
            double L = 0.5;

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double red = redBuffer[i] / 10000.0;
                double nir = nirBuffer[i] / 10000.0;

                if (nir + red + L == 0)
                {
                    result[y, x] = 0;
                }
                else
                {
                    double val = ((nir - red) / (nir + red + L)) * (1.0 + L);
                    result[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return result;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    /// <summary>
    /// Funkcja przeliczająca NDWI (Normalized Difference Water Index / NDMI)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca NDWI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static double[,] CalculateNdwi(byte[] tiffBytes)
    {
        Gdal.AllRegister();
        string memPath = $"/vsimem/ndwi_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var bandNir = ds.GetRasterBand(7);  // Kanał 7: B08 (NIR)
            using var bandSwir = ds.GetRasterBand(9); // Kanał 9: B11 (SWIR)

            int[] nirBuffer = new int[width * height];
            int[] swirBuffer = new int[width * height];

            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);
            bandSwir.ReadRaster(0, 0, width, height, swirBuffer, width, height, 0, 0);

            double[,] result = new double[height, width];

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double nir = nirBuffer[i];
                double swir = swirBuffer[i];

                if (nir + swir == 0)
                {
                    result[y, x] = 0;
                }
                else
                {
                    double val = (nir - swir) / (nir + swir);
                    result[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return result;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    /// <summary>
    /// Funkcja przeliczająca EVI (Enhanced Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca EVI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static double[,] CalculateEvi(byte[] tiffBytes)
    {
        Gdal.AllRegister();
        string memPath = $"/vsimem/evi_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var bandBlue = ds.GetRasterBand(1); // Kanał 1: B02 (Blue)
            using var bandRed = ds.GetRasterBand(3);  // Kanał 3: B04 (Red)
            using var bandNir = ds.GetRasterBand(7);  // Kanał 7: B08 (NIR)

            int[] blueBuffer = new int[width * height];
            int[] redBuffer = new int[width * height];
            int[] nirBuffer = new int[width * height];

            bandBlue.ReadRaster(0, 0, width, height, blueBuffer, width, height, 0, 0);
            bandRed.ReadRaster(0, 0, width, height, redBuffer, width, height, 0, 0);
            bandNir.ReadRaster(0, 0, width, height, nirBuffer, width, height, 0, 0);

            double[,] result = new double[height, width];

            for (int i = 0; i < width * height; i++)
            {
                int y = i / width;
                int x = i % width;

                double blue = blueBuffer[i] / 10000.0;
                double red = redBuffer[i] / 10000.0;
                double nir = nirBuffer[i] / 10000.0;

                double denominator = nir + (6.0 * red) - (7.5 * blue) + 1.0;

                if (denominator == 0)
                {
                    result[y, x] = 0;
                }
                else
                {
                    double val = 2.5 * ((nir - red) / denominator);
                    result[y, x] = Math.Clamp(val, -1.0, 1.0);
                }
            }

            return result;
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }
    #endregion
}
