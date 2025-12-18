using OSGeo.GDAL;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Utils;
using WebApplication1.DAL; // Dodajemy namespace DAL

namespace WebApplication1.Services
{
    public class AnalysisService
    {
        // Import funkcji C z biblioteki GDAL do obsługi pamięci RAM (vsimem)
        [DllImport("gdal.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr VSIGetMemFileBuffer(string filename, out long size, int unref);

        // Zmieniamy DatabaseService na konkretne DALe
        private readonly ScanDAL _scanDal;
        private readonly FieldDAL _fieldDal; // Potrzebny do pobrania progów (Thresholds)
        private readonly PythonService _pythonService;

        public AnalysisService(ScanDAL scanDal, FieldDAL fieldDal, PythonService pythonService)
        {
            _scanDal = scanDal;
            _fieldDal = fieldDal;
            _pythonService = pythonService;
        }

        // =================================================================
        // 1. OBSŁUGA SKANÓW (Pobieranie i Wizualizacja)
        // =================================================================

        public async Task<(byte[]? PngBytes, DateTime? Date)> GetVisualizedScanAsync(int fieldId, int? scanId, string geojson)
        {
            // Używamy ScanDAL do pobrania danych
            ScanResultDto? scan = scanId.HasValue
                ? await _scanDal.GetScanByIdAsync(scanId.Value)
                : await _scanDal.GetLatestScanAsync(fieldId);

            if (scan == null || scan.ImageBytes == null || scan.ImageBytes.Length == 0)
                return (null, null);

            // Reszta logiki przetwarzania obrazu pozostaje bez zmian (ImageUtils)
            var rgbImage = ImageUtils.ConvertTiffToPng(scan.ImageBytes);
            var finalImage = ImageUtils.DrawGeoJsonPolygonOnImage(rgbImage, geojson, scan.FieldBbox, false);

            return (finalImage, scan.ScanDate);
        }

        public async Task<(NdviDataDto? Data, DateTime? Date)> GetNdviDataAsync(int fieldId, int? scanId)
        {
            // Używamy ScanDAL
            ScanResultDto? scan = scanId.HasValue
                ? await _scanDal.GetScanByIdAsync(scanId.Value)
                : await _scanDal.GetLatestScanAsync(fieldId);

            if (scan == null || scan.ImageBytes == null) return (null, null);

            // Logika NDVI bez zmian
            double[,] ndviMatrix = NdviUtils.CalculateNdvi(scan.ImageBytes);
            var ndviList = ImageUtils.ConvertToNestedList(ndviMatrix);

            return (new NdviDataDto(ndviList, scan.FieldBbox), scan.ScanDate);
        }

        public byte[] RenderNdviVisualization(NdviVisualizationDto dto)
        {
            // Ta metoda nie korzysta z bazy, więc pozostaje identyczna
            var ndviArray = ImageUtils.ConvertFromNestedList(dto.NdviMatrix);
            var ndviMap = PlotUtils.RenderNdviHeatmap(ndviArray);

            if (!string.IsNullOrEmpty(dto.FieldBbox) && !string.IsNullOrEmpty(dto.Bbox))
            {
                ndviMap = ImageUtils.DrawGeoJsonPolygonOnImage(ndviMap, dto.FieldBbox, dto.Bbox, true);
            }
            return ndviMap;
        }

        // =================================================================
        // 2. ANALIZA RYZYKA (Grupowanie)
        // =================================================================

        public async Task<GroupingResultDto> GroupRiskAsync(NdviGroupRequest request)
        {
            // 1. Pobierz progi (TERAZ Z FIELD DAL)
            // W poprzednim kroku przenieśliśmy metody słownikowe do FieldDAL
            var thresholds = await _fieldDal.GetThresholdsAsync();

            var cycleThreshold = thresholds.FirstOrDefault(t => t.CycleId == request.CycleId);
            double minT = cycleThreshold?.MinNdvi ?? 0.2;
            double maxT = cycleThreshold?.MaxNdvi ?? 0.6;

            // 2. Reszta logiki (Matematyka + Python) pozostaje bez zmian
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

            var overlayBytes = ImageUtils.CreateRiskOverlayFromPoints(fieldPixels, finalLabels, width, height);
            var baseMap = PlotUtils.RenderNdviHeatmap(ndviMatrix);
            var combinedMap = ImageUtils.CombineImages(baseMap, overlayBytes);
            var finalImage = ImageUtils.DrawGeoJsonPolygonOnImage(combinedMap, request.FieldGeojson, request.ImageBbox, true);
            var legendBytes = ImageUtils.CreateLegendWithClusters(ndviMedians, clusterIds, request.DarkMode);

            return new GroupingResultDto(
                Convert.ToBase64String(finalImage),
                Convert.ToBase64String(legendBytes)
            );
        }

        // =================================================================
        // 3. IMPORT PLIKÓW (GDAL & ZIP)
        // =================================================================

        public async Task ProcessAndSaveScanAsync(int fieldId, UpdateTiffsDto data)
        {
            Gdal.AllRegister();
            string[] expectedBands = { "B02", "B03", "B04", "B08" };
            var bandDatasets = new Dictionary<string, Dataset>();
            string outPath = $"/vsimem/merged_{fieldId}_{Guid.NewGuid()}.tif";

            try
            {
                // LOGIKA GDAL (ZIP -> MemoryStream -> Vsimem) - BEZ ZMIAN
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

                // Merge pasm
                var b02 = bandDatasets["B02"];
                int xSize = b02.RasterXSize;
                int ySize = b02.RasterYSize;
                var driver = Gdal.GetDriverByName("GTiff");

                using var outDs = driver.Create(outPath, xSize, ySize, 4, DataType.GDT_Int16, new[] { "INTERLEAVE=PIXEL" });
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

                var bboxObj = CalculateBbox(outDs);
                string bboxJson = JsonSerializer.Serialize(bboxObj);

                bool isWithin = GeoUtils.IsFieldWithinRaster(bboxJson, data.Geojson);
                if (!isWithin) throw new Exception("Skan nie pokrywa się z polem.");

                long size;
                IntPtr ptr = VSIGetMemFileBuffer(outPath, out size, 0);
                if (ptr == IntPtr.Zero) throw new Exception("Błąd GDAL: Buffer error.");
                byte[] mergedBytes = new byte[size];
                Marshal.Copy(ptr, mergedBytes, 0, (int)size);

                // ZAPIS DO BAZY - ZMIANA NA SCANDAL
                await _scanDal.SaveRasterAsync(mergedBytes, fieldId, data.Date, bboxJson);
            }
            finally
            {
                // Sprzątanie GDAL - BEZ ZMIAN
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

        private Bbox CalculateBbox(Dataset ds)
        {
            double[] gt = new double[6];
            ds.GetGeoTransform(gt);
            double minX = gt[0];
            double maxY = gt[3];
            double maxX = minX + (gt[1] * ds.RasterXSize);
            double minY = maxY + (gt[5] * ds.RasterYSize);
            return new Bbox { MinX = minX, MaxX = maxX, MinY = minY, MaxY = maxY };
        }
    }
}