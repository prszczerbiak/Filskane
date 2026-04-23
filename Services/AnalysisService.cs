using OSGeo.GDAL;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Filskane.Models;
using Filskane.Utils;
using Filskane.DAL;

namespace Filskane.Services;

/// <summary>
/// Serwis odpowiedzialny za zaawansowaną analizę obrazów satelitarnych, obliczanie wskaźników (NDVI) i przetwarzanie plików rastrowych.
/// </summary>
public class AnalysisService
{
    [DllImport("gdal.dll", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr VSIGetMemFileBuffer(string filename, out long size, int unref);

    private readonly ScanDAL _scanDal;
    private readonly FieldDAL _fieldDal;
    private readonly PythonService _pythonService;

    public AnalysisService(ScanDAL scanDal, FieldDAL fieldDal, PythonService pythonService)
    {
        _scanDal = scanDal;
        _fieldDal = fieldDal;
        _pythonService = pythonService;
    }

    /// <summary>
    /// Pobiera i przetwarza obraz skanu do wizualizacji (konwersja TIFF -> PNG).
    /// Opcjonalnie nakłada granice pola (polygon) na obraz.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID konkretnego skanu (opcjonalne, domyślnie najnowszy).</param>
    /// <param name="geojson">Geometria pola (GeoJSON) do wyrysowania granic.</param>
    /// <returns>Krotka zawierająca bajty obrazu PNG i datę skanu.</returns>
    public async Task<(byte[]? PngBytes, DateTime? Date)> GetVisualizedScanAsync(string username, int fieldId, int? scanId, string geojson)
    {
        ScanResultDto? scan = scanId.HasValue
            ? await _scanDal.GetScanByIdAsync(username, scanId.Value)
            : await _scanDal.GetLatestScanAsync(username, fieldId);

        if (scan == null || scan.ImageBytes == null || scan.ImageBytes.Length == 0)
            return (null, null);

        var rgbImage = ImageUtils.ConvertTiffToPng(scan.ImageBytes);

        if (scan.FieldBbox != null)
        {
            rgbImage = ImageUtils.DrawGeoJsonPolygonOnImage(rgbImage, geojson, scan.FieldBbox, false);
        }

        return (rgbImage, scan.ScanDate);
    }

    /// <summary>
    /// Pobiera surowe dane numeryczne NDVI dla skanu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID skanu (opcjonalne).</param>
    /// <returns>Obiekt z macierzą NDVI i datą skanu.</returns>
    public async Task<(NdviDataDto? Data, DateTime? Date)> GetNdviDataAsync(string username, int fieldId, int? scanId)
        => await GetVegetationDataAsync(username, fieldId, scanId, NdviUtils.CalculateNdvi);

    /// <summary>
    /// Pobiera surowe dane numeryczne GNDVI dla skanu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID skanu (opcjonalne).</param>
    /// <returns>Obiekt z macierzą GNDVI i datą skanu.</returns>
    public async Task<(NdviDataDto? Data, DateTime? Date)> GetGndviDataAsync(string username, int fieldId, int? scanId)
        => await GetVegetationDataAsync(username, fieldId, scanId, NdviUtils.CalculateGndvi);

    /// <summary>
    /// Pobiera surowe dane numeryczne SAVI dla skanu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID skanu (opcjonalne).</param>
    /// <returns>Obiekt z macierzą SAVI i datą skanu.</returns>
    public async Task<(NdviDataDto? Data, DateTime? Date)> GetSaviDataAsync(string username, int fieldId, int? scanId)
        => await GetVegetationDataAsync(username, fieldId, scanId, NdviUtils.CalculateSavi);

    /// <summary>
    /// Pobiera surowe dane numeryczne NDWI dla skanu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID skanu (opcjonalne).</param>
    /// <returns>Obiekt z macierzą NDWI i datą skanu.</returns>
    public async Task<(NdviDataDto? Data, DateTime? Date)> GetNdwiDataAsync(string username, int fieldId, int? scanId)
        => await GetVegetationDataAsync(username, fieldId, scanId, NdviUtils.CalculateNdwi);

    /// <summary>
    /// Pobiera surowe dane numeryczne EVI dla skanu.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="scanId">ID skanu (opcjonalne).</param>
    /// <returns>Obiekt z macierzą EVI i datą skanu.</returns>
    public async Task<(NdviDataDto? Data, DateTime? Date)> GetEviDataAsync(string username, int fieldId, int? scanId)
        => await GetVegetationDataAsync(username, fieldId, scanId, NdviUtils.CalculateEvi);

    private async Task<(NdviDataDto? Data, DateTime? Date)> GetVegetationDataAsync(
        string username,
        int fieldId,
        int? scanId,
        Func<byte[], double[,]> calculator)
    {
        ScanResultDto? scan = scanId.HasValue
            ? await _scanDal.GetScanByIdAsync(username, scanId.Value)
            : await _scanDal.GetLatestScanAsync(username, fieldId);

        if (scan == null || scan.ImageBytes == null) return (null, null);

        double[,] vegetationMatrix = calculator(scan.ImageBytes);
        var vegetationList = ImageUtils.ConvertToNestedList(vegetationMatrix);

        return (new NdviDataDto(vegetationList, scan.FieldBbox), scan.ScanDate);
    }

    /// <summary>
    /// Generuje mapę cieplną (heatmap) NDVI na podstawie dostarczonej macierzy danych.
    /// Metoda bezstanowa (nie korzysta z bazy danych).
    /// </summary>
    /// <param name="dto">Dane wejściowe (macierz NDVI, bbox).</param>
    /// <returns>Obraz PNG z wizualizacją NDVI.</returns>
    public byte[] RenderIndexVisualization(IndexVisualizationDto dto)
    {
        var indexArray = ImageUtils.ConvertFromNestedList(dto.IndexMatrix);
        var analysisType = dto.AnalysisType?.Trim().ToUpperInvariant();
        var indexMap = analysisType == "NDWI"
            ? PlotUtils.RenderNDWIHeatmap(indexArray)
            : PlotUtils.RenderVegetationHeatmap(indexArray);

        if ((!string.IsNullOrEmpty(dto.FieldBbox)) && dto.Bbox != null)
        {
            indexMap = ImageUtils.DrawGeoJsonPolygonOnImage(indexMap, dto.FieldBbox, dto.Bbox, true);
        }
        return indexMap;
    }

    /// <summary>
    /// Przeprowadza analizę ryzyka uprawowego z wykorzystaniem algorytmu DBSCAN (Python).
    /// Grupuje obszary o niskim NDVI w klastry ryzyka.
    /// </summary>
    /// <param name="request">Dane wejściowe do analizy (NDVI, cykl, progi).</param>
    /// <returns>Obiekt zawierający obrazy mapy i legendy zakodowane w Base64.</returns>
    public async Task<GroupingResultDto> GroupRiskAsync(NdviGroupRequestDto request)
    {
        var analysisType = string.IsNullOrWhiteSpace(request.AnalysisType)
            ? "NDVI"
            : request.AnalysisType.Trim().ToUpperInvariant();

        var threshold = await _fieldDal.GetThresholdAsync(request.PlantId, request.CycleId, analysisType);
        double minT = threshold?.MinNdvi ?? 0.2;
        double maxT = threshold?.MaxNdvi ?? 0.6;
        Console.WriteLine($"Using thresholds for {analysisType} - Min: {minT}, Max: {maxT}");

        double[,] ndviMatrix = ImageUtils.ConvertFromNestedList(request.VegetationIndex);
        int width = ndviMatrix.GetLength(1);
        int height = ndviMatrix.GetLength(0);

        double[][] fieldPixels = GeoUtils.GetPixelsInsidePolygonAsArray(request.FieldGeojson, request.ImageBbox, width, height);

        int[] labels = NdviClassifier.ClassifyPoints(fieldPixels, ndviMatrix, minT, maxT);

        var pointsForDbscan = fieldPixels.Where((p, idx) => labels[idx] == 2).ToArray();
        var ndviForDbscan = fieldPixels
            .Where((p, idx) => labels[idx] == 2)
            .Select(p => ndviMatrix[(int)p[1], (int)p[0]])
            .ToArray();

        var (clusterIds, ndviMedians) = _pythonService.RunDbscan(pointsForDbscan, ndviForDbscan);

        int[] finalLabels = new int[labels.Length];
        int clusterIdx = 0;

        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == 2)
                finalLabels[i] = clusterIds[clusterIdx++];
            else
                finalLabels[i] = labels[i];
        }

        var overlayBytes = ImageUtils.CreateRiskOverlayFromPoints(fieldPixels, finalLabels, width, height, ndviMedians, minT);
        var baseMap = analysisType == "NDWI"
            ? PlotUtils.RenderNDWIHeatmap(ndviMatrix)
            : PlotUtils.RenderVegetationHeatmap(ndviMatrix);
        var combinedMap = ImageUtils.CombineImages(baseMap, overlayBytes);
        var finalImage = ImageUtils.DrawGeoJsonPolygonOnImage(combinedMap, request.FieldGeojson, request.ImageBbox, true);
        var legendBytes = ImageUtils.CreateLegendWithClusters(ndviMedians, clusterIds, minT, request.DarkMode);

        return new GroupingResultDto(
            Convert.ToBase64String(finalImage),
            Convert.ToBase64String(legendBytes)
        );
    }

    /// <summary>
    /// Przetwarza przesłany plik ZIP ze skanami satelitarnymi (Sentinel-2), scala kanały spektralne (B02, B03, B04, B08)
    /// i zapisuje wynikowy obraz GeoTIFF w bazie danych.
    /// </summary>
    /// <param name="username">Nazwa użytkownika.</param>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="data">Plik ZIP i metadane.</param>
    /// <exception cref="Exception">Rzucany w przypadku błędów GDAL lub braku wymaganych pasm.</exception>
    public async Task ProcessAndSaveScanAsync(string username, int fieldId, UpdateTiffsDto data)
    {
        Gdal.AllRegister();

        // ZMIANA 1: Nowa, pełna lista pasm dla rolnictwa (10 kanałów)
        string[] expectedBands = { "B02", "B03", "B04", "B05", "B06", "B07", "B08", "B8A", "B11", "B12" };

        var bandDatasets = new Dictionary<string, Dataset>();
        string outPath = $"/vsimem/merged_{fieldId}_{Guid.NewGuid()}.tif";

        try
        {
            using var zipStream = new MemoryStream();
            await data.Zip.CopyToAsync(zipStream);
            zipStream.Position = 0;
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) &&
                    !entry.FullName.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase)) continue;

                string nameUpper = Path.GetFileNameWithoutExtension(entry.Name).ToUpper();

                // Szukamy dopasowania, ale ostrożnie (np. żeby "B08" nie złapało "B8A")
                var bandName = expectedBands.FirstOrDefault(b => nameUpper.Contains(b + "_") || nameUpper.EndsWith(b));
                if (bandName == null) continue;

                using var bandStream = entry.Open();
                using var ms = new MemoryStream();
                await bandStream.CopyToAsync(ms);
                byte[] bytes = ms.ToArray();

                string vsiPath = $"/vsimem/{fieldId}_{bandName}_{Guid.NewGuid()}.tif";
                Gdal.FileFromMemBuffer(vsiPath, bytes);
                var ds = Gdal.Open(vsiPath, Access.GA_ReadOnly);
                if (ds == null) throw new Exception($"Nie udało się otworzyć pasma {bandName}");
                bandDatasets[bandName] = ds;
            }

            var missing = expectedBands.Where(b => !bandDatasets.ContainsKey(b)).ToList();
            if (missing.Any()) throw new Exception($"Brakuje pasm: {string.Join(", ", missing)}");

            // B02 będzie naszym wzorcem dla najwyższej rozdzielczości (10m)
            var b02 = bandDatasets["B02"];
            int targetXSize = b02.RasterXSize;
            int targetYSize = b02.RasterYSize;
            var driver = Gdal.GetDriverByName("GTiff");

            // ZMIANA 2: Dynamiczna ilość kanałów na podstawie expectedBands.Length
            using var outDs = driver.Create(outPath, targetXSize, targetYSize, expectedBands.Length, DataType.GDT_Int16, ["INTERLEAVE=PIXEL"]);

            for (int i = 0; i < expectedBands.Length; i++)
            {
                var srcDs = bandDatasets[expectedBands[i]];
                var srcBand = srcDs.GetRasterBand(1);

                // ZMIANA 3: Odczyt oryginalnych wymiarów aktualnego pasma (może to być 20m)
                int srcXSize = srcBand.XSize;
                int srcYSize = srcBand.YSize;

                float[] buffer = new float[targetXSize * targetYSize];

                // MAGIA GDAL: Jeśli srcX/Y są mniejsze niż targetX/Y, GDAL sam przeskaluje obraz w pamięci!
                srcBand.ReadRaster(0, 0, srcXSize, srcYSize, buffer, targetXSize, targetYSize, 0, 0);

                outDs.GetRasterBand(i + 1).WriteRaster(0, 0, targetXSize, targetYSize, buffer, targetXSize, targetYSize, 0, 0);
            }

            double[] geoTransform = new double[6];
            b02.GetGeoTransform(geoTransform);
            outDs.SetGeoTransform(geoTransform);
            string projection = b02.GetProjectionRef();
            if (!string.IsNullOrEmpty(projection)) outDs.SetProjection(projection);
            outDs.FlushCache();

            var bboxObj = Bbox.FromGdal(outDs);

            bool isWithin = GeoUtils.IsFieldWithinRaster(bboxObj, data.Geojson);
            if (!isWithin) throw new Exception("Skan nie pokrywa się z polem.");

            long size;
            IntPtr ptr = VSIGetMemFileBuffer(outPath, out size, 0);
            if (ptr == IntPtr.Zero) throw new Exception("Błąd GDAL: Buffer error.");
            byte[] mergedBytes = new byte[size];
            Marshal.Copy(ptr, mergedBytes, 0, (int)size);

            string bboxJsonToSave = bboxObj.ToString();

            await _scanDal.SaveRasterAsync(username, mergedBytes, fieldId, data.Date, bboxJsonToSave);
        }
        finally
        {
            Gdal.Unlink(outPath);
            foreach (var kv in bandDatasets)
            {
                if (kv.Value != null)
                {
                    string dsDesc = kv.Value.GetDescription();
                    kv.Value.Dispose();
                    if (!string.IsNullOrEmpty(dsDesc) && dsDesc.StartsWith("/vsimem/"))
                    {
                        Gdal.Unlink(dsDesc);
                    }
                }
            }
        }
    }
}
