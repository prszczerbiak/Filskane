using OSGeo.GDAL;

namespace Filskane.Utils;
/// <summary>
/// Klasa pomocnicza dotycząca NDVI
/// </summary>
public static class NdviUtils
{

    private static (float[] Data, int Width, int Height) CalculateTwoBandIndex(
    byte[] tiffBytes, 
    int band1Id, 
    int band2Id, 
    Func<float, float, float> formula)
    {
        string memPath = $"/vsimem/index_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var band1 = ds.GetRasterBand(band1Id);
            using var band2 = ds.GetRasterBand(band2Id);

            float[] result = new float[height * width];
            object gdalLock = new object();

            Parallel.For(0, height, y =>
            {
                short[] row1 = System.Buffers.ArrayPool<short>.Shared.Rent(width);
                short[] row2 = System.Buffers.ArrayPool<short>.Shared.Rent(width);

                try
                {
                    lock (gdalLock)
                    {
                        band1.ReadRaster(0, y, width, 1, row1, width, 1, 0, 0);
                        band2.ReadRaster(0, y, width, 1, row2, width, 1, 0, 0);
                    }

                    for (int x = 0; x < width; x++)
                    {
                        // Od razu normalizujemy do reflektancji 0-1, żeby uprościć wzory!
                        float val1 = (ushort)row1[x] / 10000.0f;
                        float val2 = (ushort)row2[x] / 10000.0f;
                        
                        int index = y * width + x; 

                        float calculatedValue = formula(val1, val2);
                        
                        result[index] = float.IsFinite(calculatedValue) ? Math.Clamp(calculatedValue, -1.0f, 1.0f) : 0;
                    }
                }
                finally
                {
                    System.Buffers.ArrayPool<short>.Shared.Return(row1);
                    System.Buffers.ArrayPool<short>.Shared.Return(row2);
                }
            });

            return (result, width, height);
        }
        finally
        {
            Gdal.Unlink(memPath);
        }
    }

    #region Public Methods
    /// <summary>
    /// Funkcja przeliczająca NDVI (Normalized Difference Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca NDVI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static (float[] Data, int Width, int Height) CalculateNdvi(byte[] tiffBytes)
    {
        return CalculateTwoBandIndex(tiffBytes, 3, 7, (red, nir) => 
        {
            if (nir + red == 0) return 0;
            return (nir - red) / (nir + red);
        });
    }

    /// <summary>
    /// Funkcja przeliczająca Gndvi (Green Normalized Difference Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca Gndvi wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static (float[] Data, int Width, int Height) CalculateGndvi(byte[] tiffBytes)
    {
        return CalculateTwoBandIndex(tiffBytes, 2, 7, (green, nir) => 
        {
            if (nir + green == 0) return 0;
            return (nir - green) / (nir + green);
        });
    }

    /// <summary>
    /// Funkcja przeliczająca Savi (Soil Adjusted Vegetation Index)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca Savi wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static (float[] Data, int Width, int Height) CalculateSavi(byte[] tiffBytes)
    {
        return CalculateTwoBandIndex(tiffBytes, 2, 7, (green, nir) => 
        {
            float L = 0.5f;
            if (nir + green + L == 0) return 0;
            return ((nir - green) / (nir + green + L)) * (1.0f + L);
        });
    }


    /// <summary>
    /// Funkcja przeliczająca NDWI (Normalized Difference Water Index / NDMI)
    /// </summary>
    /// <param name="tiffBytes">Tablica bajtowa zawierająca potrzebne m.in. pasma</param>
    /// <returns>Macierz liczb typu zmiennoprzecinkowego podwójnej precyzji, zawierająca NDWI wejściowego obrazu</returns>
    /// <exception cref="Exception"></exception>
    public static (float[] Data, int Width, int Height) CalculateNdwi(byte[] tiffBytes)
    {
        return CalculateTwoBandIndex(tiffBytes, 7, 9, (nir, swir) => 
        {
            if (nir + swir == 0) return 0;
            return (nir - swir) / (nir + swir);
        });
    }

    private static (float[] Data, int Width, int Height) CalculateThreeBandIndex(
    byte[] tiffBytes,
    int band1Id,
    int band2Id,
    int band3Id,
    Func<float, float, float, float> formula)
    {
        string memPath = $"/vsimem/index_calc_{Guid.NewGuid()}.tif";

        try
        {
            Gdal.FileFromMemBuffer(memPath, tiffBytes);
            using var ds = Gdal.Open(memPath, Access.GA_ReadOnly);
            if (ds == null) throw new Exception("Nie można otworzyć obrazu przez GDAL.");

            int width = ds.RasterXSize;
            int height = ds.RasterYSize;

            using var band1 = ds.GetRasterBand(band1Id);
            using var band2 = ds.GetRasterBand(band2Id);
            using var band3 = ds.GetRasterBand(band3Id);

            float[] result = new float[height * width];
            object gdalLock = new object();

            Parallel.For(0, height, y =>
            {
                // Wypożyczamy 3 tablice
                short[] row1 = System.Buffers.ArrayPool<short>.Shared.Rent(width);
                short[] row2 = System.Buffers.ArrayPool<short>.Shared.Rent(width);
                short[] row3 = System.Buffers.ArrayPool<short>.Shared.Rent(width);

                try
                {
                    lock (gdalLock)
                    {
                        band1.ReadRaster(0, y, width, 1, row1, width, 1, 0, 0);
                        band2.ReadRaster(0, y, width, 1, row2, width, 1, 0, 0);
                        band3.ReadRaster(0, y, width, 1, row3, width, 1, 0, 0);
                    }

                    for (int x = 0; x < width; x++)
                    {
                        // Normalizujemy wszystkie trzy kanały
                        float val1 = (ushort)row1[x] / 10000.0f;
                        float val2 = (ushort)row2[x] / 10000.0f;
                        float val3 = (ushort)row3[x] / 10000.0f;

                        int index = y * width + x;

                        // WYZWOŁANIE DELEGATA (Kolejność musi się zgadzać z argumentami wywołania!)
                        float calculatedValue = formula(val1, val2, val3);

                        result[index] = float.IsFinite(calculatedValue) ? Math.Clamp(calculatedValue, -1.0f, 1.0f) : 0;
                    }
                }
                finally
                {
                    // Zwracamy wszystkie 3 tablice
                    System.Buffers.ArrayPool<short>.Shared.Return(row1);
                    System.Buffers.ArrayPool<short>.Shared.Return(row2);
                    System.Buffers.ArrayPool<short>.Shared.Return(row3);
                }
            });

            return (result, width, height);
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
    public static (float[] Data, int Width, int Height) CalculateEvi(byte[] tiffBytes)
    {
        // Pasma: 1 (Blue), 3 (Red), 7 (NIR)
        return CalculateThreeBandIndex(tiffBytes, 1, 3, 7, (blue, red, nir) => 
        {
            // Mianownik wzoru EVI
            float denominator = nir + (6.0f * red) - (7.5f * blue) + 1.0f;

            if (denominator == 0f) 
            {
                return 0f;
            }

            return 2.5f * ((nir - red) / denominator);
        });
    }
    #endregion
}
