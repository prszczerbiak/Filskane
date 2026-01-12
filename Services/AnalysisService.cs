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
    {
        ScanResultDto? scan = scanId.HasValue
            ? await _scanDal.GetScanByIdAsync(username, scanId.Value)
            : await _scanDal.GetLatestScanAsync(username, fieldId);

        if (scan == null || scan.ImageBytes == null) return (null, null);

        double[,] ndviMatrix = NdviUtils.CalculateNdvi(scan.ImageBytes);
        var ndviList = ImageUtils.ConvertToNestedList(ndviMatrix);

        return (new NdviDataDto(ndviList, scan.FieldBbox), scan.ScanDate);
    }

    /// <summary>
    /// Generuje mapę cieplną (heatmap) NDVI na podstawie dostarczonej macierzy danych.
    /// Metoda bezstanowa (nie korzysta z bazy danych).
    /// </summary>
    /// <param name="dto">Dane wejściowe (macierz NDVI, bbox).</param>
    /// <returns>Obraz PNG z wizualizacją NDVI.</returns>
    public byte[] RenderNdviVisualization(NdviVisualizationDto dto)
    {
        var ndviArray = ImageUtils.ConvertFromNestedList(dto.NdviMatrix);
        var ndviMap = PlotUtils.RenderNdviHeatmap(ndviArray);

        if ((!string.IsNullOrEmpty(dto.FieldBbox)) && dto.Bbox != null)
        {
            ndviMap = ImageUtils.DrawGeoJsonPolygonOnImage(ndviMap, dto.FieldBbox, dto.Bbox, true);
        }
        return ndviMap;
    }

    /// <summary>
    /// Przeprowadza analizę ryzyka uprawowego z wykorzystaniem algorytmu DBSCAN (Python).
    /// Grupuje obszary o niskim NDVI w klastry ryzyka.
    /// </summary>
    /// <param name="request">Dane wejściowe do analizy (NDVI, cykl, progi).</param>
    /// <returns>Obiekt zawierający obrazy mapy i legendy zakodowane w Base64.</returns>
    public async Task<GroupingResultDto> GroupRiskAsync(NdviGroupRequestDto request)
    {
        var thresholds = await _fieldDal.GetThresholdsAsync();

        var cycleThreshold = thresholds.FirstOrDefault(t => t.CycleId == request.CycleId);
        double minT = cycleThreshold?.MinNdvi ?? 0.2;
        double maxT = cycleThreshold?.MaxNdvi ?? 0.6;

        double[,] ndviMatrix = ImageUtils.ConvertFromNestedList(request.Ndvi);
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
        var baseMap = PlotUtils.RenderNdviHeatmap(ndviMatrix);
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
        string[] expectedBands = { "B02", "B03", "B04", "B08" };
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
                var bandName = expectedBands.FirstOrDefault(b => nameUpper.Contains(b));
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

            var b02 = bandDatasets["B02"];
            int xSize = b02.RasterXSize;
            int ySize = b02.RasterYSize;
            var driver = Gdal.GetDriverByName("GTiff");

            using var outDs = driver.Create(outPath, xSize, ySize, 4, DataType.GDT_Int16, ["INTERLEAVE=PIXEL"]);
            for (int i = 0; i < expectedBands.Length; i++)
            {
                var srcDs = bandDatasets[expectedBands[i]];
                var srcBand = srcDs.GetRasterBand(1);
                float[] buffer = new float[xSize * ySize];
                srcBand.ReadRaster(0, 0, xSize, ySize, buffer, xSize, ySize, 0, 0);
                outDs.GetRasterBand(i + 1).WriteRaster(0, 0, xSize, ySize, buffer, xSize, ySize, 0, 0);
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
