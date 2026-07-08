using OSGeo.GDAL;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Filskane.Models;
using Filskane.Utils;
using Filskane.DAL;
using System.Text.Json;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

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
    private readonly ReportDAL _reportDal;
    private readonly PythonService _pythonService;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;

    public AnalysisService(ScanDAL scanDal, FieldDAL fieldDal, ReportDAL reportDal, PythonService pythonService, EmailService emailService, IConfiguration configuration)
    {
        _scanDal = scanDal;
        _fieldDal = fieldDal;
        _reportDal = reportDal;
        _pythonService = pythonService;
        _emailService = emailService;
        _configuration = configuration;
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
    public async Task<(byte[]? PngBytes, DateTime? Date)> GetVisualizedScanAsync(string username, int fieldId, int? scanId, JsonElement geojson)
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

    public async Task<(byte[]? PngBytes, DateTime? Date)> PrepareOverallAnalysisAsync(string username, int fieldId, JsonElement geojson)
    {
        var analysis = await BuildOverallAnalysisAsync(username, fieldId, geojson);
        return (analysis.PngBytes, analysis.Date);
    }

    public async Task<byte[]> GenerateOverallAnalysisReportAsync(string username, int fieldId, JsonElement geojson)
    {
        var analysis = await BuildOverallAnalysisAsync(username, fieldId, geojson);

        if (analysis.PngBytes == null || analysis.Date == null || analysis.Field == null)
            throw new Exception("Brak danych do wygenerowania raportu.");

        var document = new PdfDocument();
        document.Info.Title = $"Raport analizy pola {analysis.Field.Name}";
        document.Info.Author = "Filskane";

        var page = document.AddPage();
        page.Size = PdfSharp.PageSize.A4;
        page.Orientation = PdfSharp.PageOrientation.Landscape;

        using var graphics = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 20, XFontStyleEx.Bold);
        var headerFont = new XFont("Arial", 11, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var smallFont = new XFont("Arial", 9, XFontStyleEx.Regular);
        var brush = XBrushes.Black;

        double margin = 28;
        double top = 28;
        double leftColumnWidth = 245;
        double rightColumnX = margin + leftColumnWidth + 18;
        double rightColumnWidth = page.Width - rightColumnX - margin;

        // --- NOWA LOGIKA: Obliczanie daty siewu i dni wegetacji ---
        string sowingDateText = analysis.Field.SowingDate.HasValue
            ? analysis.Field.SowingDate.Value.ToString("dd.MM.yyyy")
            : "Brak danych";

        string growthDaysText = "Brak danych";
        if (analysis.Field.SowingDate.HasValue && analysis.Date.HasValue)
        {
            // Obliczamy różnicę w dniach między skanem a siewem
            int days = (int)(analysis.Date.Value.Date - analysis.Field.SowingDate.Value.Date).TotalDays;
            growthDaysText = days >= 0 ? $"{days} dni" : "Przed siewem";
        }

        graphics.DrawString("Raport analizy całościowej", titleFont, brush, new XPoint(margin, top + 2));
        graphics.DrawString($"Pole: {analysis.Field.Name}", headerFont, brush, new XPoint(margin, top + 32));
        graphics.DrawString($"Data skanu: {analysis.Date:dd.MM.yyyy}", bodyFont, brush, new XPoint(margin, top + 52));
        graphics.DrawString($"Uprawa: {analysis.Field.PlantName ?? "Brak danych"}", bodyFont, brush, new XPoint(margin, top + 68));
        graphics.DrawString($"Data siewu: {sowingDateText}", bodyFont, brush, new XPoint(margin, top + 84));
        graphics.DrawString($"Czas wegetacji (do skanu): {growthDaysText}", bodyFont, brush, new XPoint(margin, top + 100));
        graphics.DrawString($"Faza rozwoju: {analysis.Field.CycleName ?? "Brak danych"}", bodyFont, brush, new XPoint(margin, top + 116));
        graphics.DrawString($"Powierzchnia: {FormatArea(analysis.Field.Area)}", bodyFont, brush, new XPoint(margin, top + 132));
        graphics.DrawString($"Gleba: {analysis.Field.SoilComplex ?? "Brak danych"} / {analysis.Field.SoilType ?? "Brak danych"}", bodyFont, brush, new XPoint(margin, top + 148));
        graphics.DrawString($"Podłoże: {analysis.Field.SoilSubstrate ?? "Brak danych"}", bodyFont, brush, new XPoint(margin, top + 164));
        graphics.DrawString("Podsumowanie klas", headerFont, brush, new XPoint(margin, top + 197));

        double summaryY = top + 218;
        foreach (var line in BuildSummaryLines(analysis.ClassCounts, analysis.Field.Area))
        {
            graphics.DrawString(line, smallFont, brush, new XPoint(margin, summaryY));
            summaryY += 16;
        }

        graphics.DrawString("Wizualizacja analizy", headerFont, brush, new XPoint(rightColumnX, top + 2));
        using var imageStream = new MemoryStream(analysis.PngBytes);
        using var pdfImage = XImage.FromStream(imageStream);

        double maxImageHeight = page.Height - top - 58;
        double maxImageWidth = rightColumnWidth;
        double scale = Math.Min(maxImageWidth / pdfImage.PixelWidth, maxImageHeight / pdfImage.PixelHeight);
        double imageWidth = pdfImage.PixelWidth * scale;
        double imageHeight = pdfImage.PixelHeight * scale;
        double imageX = rightColumnX + (maxImageWidth - imageWidth) / 2;
        double imageY = top + 24;

        graphics.DrawRectangle(XPens.Gray, imageX - 2, imageY - 2, imageWidth + 4, imageHeight + 4);
        graphics.DrawImage(pdfImage, imageX, imageY, imageWidth, imageHeight);

        var footerText = "Raport wygenerowano automatycznie na podstawie najnowszego skanu i całościowej analizy pola.";
        graphics.DrawString(footerText, smallFont, brush, new XRect(margin, page.Height - 28, page.Width - margin * 2, 14), XStringFormats.CenterLeft);

        using var output = new MemoryStream();
        document.Save(output, false);
        return output.ToArray();
    }

    public async Task<byte[]> SendOverallAnalysisReportAsync(string username, int fieldId, JsonElement geojson, string recipientEmail)
    {
        var reportBytes = await GenerateOverallAnalysisReportAsync(username, fieldId, geojson);
        var field = await _fieldDal.GetUserFieldByIdAsync(username, fieldId)
            ?? throw new Exception("Nie znaleziono pola.");
        var userId = await _reportDal.GetUserIdAsync(username)
            ?? throw new Exception("Nie znaleziono użytkownika.");

        var subject = $"Raport analizy całościowej pola {field.Name}";
        var placeholderReportId = await _reportDal.SaveReportAsync(userId, System.Text.Json.JsonSerializer.Serialize(new
        {
            fieldId,
            fieldName = field.Name,
            recipientEmail,
            generatedAt = DateTime.UtcNow,
            subject,
            body = "Raport został wysłany do walidacji.",
            pdfBase64 = Convert.ToBase64String(reportBytes)
        }));

        var validationUrl = $"{_configuration["AppUrl"]}/api/field/overallAnalysisReport/validate/{placeholderReportId}";

        var body = $@"
            <html>
                <body style='font-family: Arial, sans-serif;'>
                    <h3>Raport analizy całościowej</h3>
                    <p>W załączniku znajduje się raport PDF dla pola <strong>{field.Name}</strong>.</p>
                    <p>
                        <a href='{validationUrl}' style='display:inline-block;padding:12px 18px;background:#2e7d32;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;'>
                            Zatwierdź raport
                        </a>
                    </p>
                    <p>Wysłano automatycznie z systemu Filskane.</p>
                </body>
            </html>";

        var reportRecord = System.Text.Json.JsonSerializer.Serialize(new
        {
            reportId = placeholderReportId,
            fieldId,
            fieldName = field.Name,
            recipientEmail,
            generatedAt = DateTime.UtcNow,
            subject,
            body,
            validationUrl,
            pdfBase64 = Convert.ToBase64String(reportBytes)
        });

        await _reportDal.UpdateReportContentAsync(placeholderReportId, reportRecord);

        await _emailService.SendEmailWithAttachmentAsync(
            recipientEmail,
            subject,
            body,
            reportBytes,
            $"raport-analizy-pola-{fieldId}-{DateTime.Now:yyyyMMdd-HHmm}.pdf");

        return reportBytes;
    }

    public async Task<bool> ValidateOverallAnalysisReportAsync(int reportId)
        => await _reportDal.MarkReportAsValidatedAsync(reportId);

    public async Task<bool> RejectOverallAnalysisReportAsync(int reportId)
        => await _reportDal.RejectReportAsync(reportId);

    public async Task<List<UserReportDto>> GetUserReportsAsync(string username)
        => await _reportDal.GetUserReportsAsync(username);

    public async Task<List<UserReportDto>> GetPendingReportsAsync()
        => await _reportDal.GetPendingReportsAsync();

    public async Task<byte[]?> GetReportPdfAsync(int reportId)
        => await _reportDal.GetReportPdfAsync(reportId);

    private async Task<OverallAnalysisData> BuildOverallAnalysisAsync(string username, int fieldId, JsonElement geojson)
    {
        ScanResultDto? scan = await _scanDal.GetLatestScanAsync(username, fieldId);

        if (scan == null || scan.ImageBytes == null || scan.ImageBytes.Length == 0)
            return new OverallAnalysisData(null, null, [], null, []);

        var field = await _fieldDal.GetUserFieldByIdAsync(username, fieldId)
            ?? throw new Exception("Nie znaleziono pola.");

        if (!field.CropId.HasValue || !field.PlantStateId.HasValue)
            throw new Exception("Brak przypisanej uprawy lub fazy rozwoju dla pola.");

        var ndvi = NdviUtils.CalculateNdvi(scan.ImageBytes);
        var ndwi = NdviUtils.CalculateNdwi(scan.ImageBytes);
        var gndvi = NdviUtils.CalculateGndvi(scan.ImageBytes);

        int height = ndvi.Height;
        int width = ndvi.Width;
        var fieldPixels = (scan.FieldBbox != null && width > 0 && height > 0)
            ? GeoUtils.GetPixelsFromInsidePolygonAsArray(field.Geojson, scan.FieldBbox, width, height)
            : [];
        var fieldPixelsForGrouping = fieldPixels
            .Select(p => new double[] { p.X, p.Y })
            .ToArray();

        var ndviThreshold = await _fieldDal.GetThresholdAsync(field.CropId.Value, field.PlantStateId.Value, "NDVI");
        var gndviThreshold = await _fieldDal.GetThresholdAsync(field.CropId.Value, field.PlantStateId.Value, "GNDVI");
        var ndwiThreshold = await _fieldDal.GetThresholdAsync(field.CropId.Value, field.PlantStateId.Value, "NDWI");

        var groupingResult = _pythonService.RunMultiIndexGrouping(
            ndvi.Data,
            gndvi.Data,
            ndwi.Data,
            width,
            height,
            fieldPixelsForGrouping,
            (float)(ndviThreshold?.MinNdvi ?? 0.2),
            (float)(ndviThreshold?.MaxNdvi ?? 0.6),
            (float)(gndviThreshold?.MinNdvi ?? 0.2),
            (float)(gndviThreshold?.MaxNdvi ?? 0.6),
            (float)(ndwiThreshold?.MinNdvi ?? 0.2),
            (float)(ndwiThreshold?.MaxNdvi ?? 0.6)
        );

        var classMatrix = groupingResult.CombinedClasses;

        var rgbImage = ImageUtils.ConvertTiffToPng(scan.ImageBytes);
        if (classMatrix.Length > 0)
        {
            var overlayBytes = ImageUtils.CreateMultiIndexOverlay(classMatrix);
            if (overlayBytes.Length > 0)
            {
                rgbImage = ImageUtils.CombineImages(rgbImage, overlayBytes);
            }
        }

        if (scan.FieldBbox != null)
        {
            rgbImage = ImageUtils.DrawGeoJsonPolygonOnImage(rgbImage, geojson, scan.FieldBbox, false);
        }

        return new OverallAnalysisData(rgbImage, scan.ScanDate, classMatrix, field, CountClasses(classMatrix));
    }

    private static Dictionary<int, int> CountClasses(int[][] classMatrix)
    {
        var counts = new Dictionary<int, int>();

        foreach (var row in classMatrix)
        {
            foreach (var value in row)
            {
                counts[value] = counts.TryGetValue(value, out var current) ? current + 1 : 1;
            }
        }

        return counts;
    }

    private static IEnumerable<string> BuildSummaryLines(Dictionary<int, int> counts, double areaM2)
    {
        var labels = new Dictionary<int, string>
        {
            [0] = "Dobry stan",
            [1] = "Zadowalający",
            [2] = "Zagrożenie NDVI",
            [3] = "Zagrożenie GNDVI",
            [4] = "Zagrożenie NDWI",
            [5] = "Zagrożenie NDWI + GNDVI"
        };

        // 1. Zliczamy wszystkie przeanalizowane piksele na polu
        int totalPixels = labels.Keys.Sum(k => counts.TryGetValue(k, out var c) ? c : 0);

        foreach (var label in labels)
        {
            counts.TryGetValue(label.Key, out var count);

            // Zabezpieczenie przed dzieleniem przez 0 w przypadku pustego skanu
            if (totalPixels == 0)
            {
                yield return $"{label.Value}: 0.00 ha (0.00%)";
                continue;
            }

            // 2. Przeliczamy na hektary (1 piksel = 100m2 = 0.01 ha)
            

            // 3. Obliczamy procentowy udział tej klasy w całym polu
            double fraction = ((double)count / totalPixels);

            double areaInHectares = fraction * areaM2 / 10000;

            double percentage = fraction * 100;


            // 4. Formaturjemy ciąg znaków do raportu (2 miejsca po przecinku)
            yield return $"{label.Value}: {areaInHectares:0.00} ha ({percentage:0.00}%)";
        }
    }

    private static string FormatArea(double areaM2)
    {
        if (areaM2 <= 0) return "0.0000 ha";
        return $"{areaM2 / 10000:0.0000} ha";
    }

    private sealed record OverallAnalysisData(
        byte[]? PngBytes,
        DateTime? Date,
        int[][] ClassMatrix,
        FieldDetailDto? Field,
        Dictionary<int, int> ClassCounts);

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
        Func<byte[], (float[] Data, int Width, int Height)> calculator)
    {
        ScanResultDto? scan = scanId.HasValue
            ? await _scanDal.GetScanByIdAsync(username, scanId.Value)
            : await _scanDal.GetLatestScanAsync(username, fieldId);

        if (scan == null || scan.ImageBytes == null) return (null, null);

        var vegetationMatrix = calculator(scan.ImageBytes);

        return (new NdviDataDto(vegetationMatrix.Data, vegetationMatrix.Width, vegetationMatrix.Height, scan.FieldBbox), scan.ScanDate);
    }

    /// <summary>
    /// Generuje mapę cieplną (heatmap) NDVI na podstawie dostarczonej macierzy danych.
    /// Metoda bezstanowa (nie korzysta z bazy danych).
    /// </summary>
    /// <param name="dto">Dane wejściowe (macierz NDVI, bbox).</param>
    /// <returns>Obraz PNG z wizualizacją NDVI.</returns>
    public byte[] RenderIndexVisualization(IndexVisualizationDto dto)
    {
        var indexArray = dto.IndexMatrix ?? Array.Empty<float>();
        int width = dto.MatrixWidth;
        int height = dto.MatrixHeight;

        if (!TryNormalizeIndexDimensions(indexArray, ref width, ref height))
            throw new ArgumentException("Brak poprawnych wymiarów macierzy NDVI.", nameof(dto.MatrixWidth));

        if (indexArray.Length != width * height)
            throw new ArgumentException("Długość macierzy NDVI nie odpowiada podanym wymiarom.");

        var analysisType = dto.AnalysisType?.Trim().ToUpperInvariant();
        var indexMap = analysisType == "NDWI"
            ? PlotUtils.RenderNDWIHeatmap(indexArray, width, height)
            : PlotUtils.RenderVegetationHeatmap(indexArray, width, height);

        if (dto.FieldGeoJson.ValueKind != JsonValueKind.Undefined && dto.FieldGeoJson.ValueKind != JsonValueKind.Null && dto.Bbox != null)
        {
            indexMap = ImageUtils.DrawGeoJsonPolygonOnImage(indexMap, dto.FieldGeoJson, dto.Bbox, true);
        }
        return indexMap;
    }

    private static bool TryNormalizeIndexDimensions(float[] indexArray, ref int width, ref int height)
    {
        if (indexArray.Length == 0)
            return false;

        if (width > 0 && height > 0)
            return indexArray.Length == width * height;

        if (width <= 0 && height <= 0)
        {
            var square = (int)Math.Sqrt(indexArray.Length);
            if (square > 0 && square * square == indexArray.Length)
            {
                width = square;
                height = square;
                return true;
            }

            return false;
        }

        if (width <= 0 && height > 0 && indexArray.Length % height == 0)
        {
            width = indexArray.Length / height;
            return true;
        }

        if (height <= 0 && width > 0 && indexArray.Length % width == 0)
        {
            height = indexArray.Length / width;
            return true;
        }

        return false;
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
        float minT = (float)(threshold?.MinNdvi ?? 0.2);
        float maxT = (float)(threshold?.MaxNdvi ?? 0.6);
        Console.WriteLine($"Using thresholds for {analysisType} - Min: {minT}, Max: {maxT}");

        float[] ndviMatrix = request.VegetationIndex;
        int width = request.MatrixWidth;
        int height = request.MatrixHeight;

        var fieldPixels = GeoUtils.GetPixelsFromInsidePolygonAsArray(request.FieldGeojson, request.ImageBbox, width, height);
        var fieldPixelsForClassification = fieldPixels.ToArray();
        var fieldPixelsForDbscan = fieldPixels
            .Select(p => new double[] { p.X, p.Y })
            .ToArray();

        int[] labels = NdviClassifier.ClassifyPoints(fieldPixelsForClassification, ndviMatrix, width, minT, maxT);

        var pointsForDbscan = fieldPixelsForDbscan.Where((p, idx) => labels[idx] == 2).ToArray();
        var ndviForDbscan = fieldPixelsForClassification
            .Where((p, idx) => labels[idx] == 2)
            .Select(p => ndviMatrix[p.Y * width + p.X])
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

        var overlayBytes = ImageUtils.CreateRiskOverlayFromPoints(fieldPixelsForDbscan, finalLabels, width, height, ndviMedians, minT);
        var baseMap = analysisType == "NDWI"
            ? PlotUtils.RenderNDWIHeatmap(ndviMatrix, width, height)
            : PlotUtils.RenderVegetationHeatmap(ndviMatrix, width, height);
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
