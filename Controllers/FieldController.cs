using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Filskane.Models;
using Filskane.Services;

namespace Filskane.Controllers;

/// <summary>
/// Kontroler obsługujący szczegółowe operacje na polach, skanach satelitarnych i analizie NDVI.
/// </summary>
[ApiController]
[Route("api/field")]
[Authorize]
public class FieldController : ControllerBase
{
    private readonly FieldService _fieldService;
    private readonly AnalysisService _analysisService;
    private readonly ILogger<FieldController> _logger;

    public FieldController(FieldService fieldService, AnalysisService analysisService, ILogger<FieldController> logger)
    {
        _fieldService = fieldService;
        _analysisService = analysisService;
        _logger = logger;
    }

    private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;

    private IActionResult OkIndexData((NdviDataDto? Data, DateTime? Date) result)
    {
        if (result.Data == null)
            return NoContent();

        Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
        return Ok(new
        {
            ndvi = result.Data.Ndvi,
            matrixWidth = result.Data.MatrixWidth,
            matrixHeight = result.Data.MatrixHeight,
            fieldBbox = result.Data.FieldBbox
        });
    }

    /// <summary>
    /// Pobiera szczegółowe informacje o wybranym polu.
    /// </summary>
    /// <param name="fieldId">Identyfikator pola.</param>
    /// <returns>Obiekt ze szczegółami pola (nazwa, uprawa, powierzchnia itp.).</returns>
    [HttpGet("getData/{fieldId}")]
    public async Task<IActionResult> GetFieldInfo(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var field = await _fieldService.GetFieldDetailsAsync(username, fieldId);
            if (field == null)
            {
                _logger.LogWarning("Użytkownik {Username} próbował pobrać nieistniejące (lub cudze) pole {FieldId}", username, fieldId);
                return NotFound("Nie znaleziono pola");
            }
            return Ok(field);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania szczegółów pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    [HttpPost("overallAnalysis/{fieldId}")]
    public async Task<IActionResult> GetOverallAnalysisBaseScan(int fieldId, [FromBody] ScanRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.PrepareOverallAnalysisAsync(username, fieldId, request.Geojson);

            if (result.PngBytes == null)
            {
                Response.Headers.Append("X-Scan-Date", "Brak danych");
                return NoContent();
            }

            Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
            return File(result.PngBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd przygotowania całościowej analizy dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("overallAnalysisReport/{fieldId}")]
    public async Task<IActionResult> GetOverallAnalysisReport(int fieldId, [FromBody] ScanRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var pdfBytes = await _analysisService.GenerateOverallAnalysisReportAsync(username, fieldId, request.Geojson);
            var fileName = $"raport-analizy-pola-{fieldId}-{DateTime.Now:yyyyMMdd-HHmm}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd generowania raportu analizy dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("overallAnalysisReport/send/{fieldId}")]
    public async Task<IActionResult> SendOverallAnalysisReport(int fieldId, [FromBody] ScanRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            const string validationEmail = "mrocznykominaj1@gmail.com";
            await _analysisService.SendOverallAnalysisReportAsync(username, fieldId, request.Geojson, validationEmail);
            return Ok(new { message = $"Raport został wysłany na {validationEmail}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd wysyłania raportu analizy dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("overallAnalysisReport/validate/{reportId}")]
    public async Task<IActionResult> ValidateOverallAnalysisReport(int reportId)
    {
        try
        {
            var updated = await _analysisService.ValidateOverallAnalysisReportAsync(reportId);
            if (!updated)
                return NotFound(new { error = "Nie znaleziono raportu." });

            return Ok(new { message = "Raport został zwalidowany." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd walidacji raportu {ReportId}", reportId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("overallAnalysisReport/reject/{reportId}")]
    public async Task<IActionResult> RejectOverallAnalysisReport(int reportId)
    {
        try
        {
            var updated = await _analysisService.RejectOverallAnalysisReportAsync(reportId);
            if (!updated)
                return NotFound(new { error = "Nie znaleziono raportu." });

            return Ok(new { message = "Raport został odrzucony." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd odrzucania raportu {ReportId}", reportId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetUserReports()
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var reports = await _analysisService.GetUserReportsAsync(username);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania raportów użytkownika {Username}", username);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("pending-reports")]
    public async Task<IActionResult> GetPendingReports()
    {
        try
        {
            var reports = await _analysisService.GetPendingReportsAsync();
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania niezatwierdzonych raportów");
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("report/{reportId}")]
    public async Task<IActionResult> DownloadReport(int reportId)
    {
        try
        {
            var pdfBytes = await _analysisService.GetReportPdfAsync(reportId);
            if (pdfBytes == null)
                return NotFound(new { error = "Nie znaleziono raportu." });

            return File(pdfBytes, "application/pdf", $"raport-{reportId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania raportu {ReportId}", reportId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Aktualizuje dane podstawowe pola (np. nazwa, typ uprawy).
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="request">Nowe dane pola.</param>
    /// <returns>Status operacji.</returns>
    [HttpPut("update/{fieldId}")]
    public async Task<IActionResult> UpdateFieldInfo(int fieldId, [FromBody] UpdateFieldRequest request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            await _fieldService.UpdateFieldAsync(username, fieldId, request);
            _logger.LogInformation("Zaktualizowano pole {FieldId} przez {Username}", fieldId, username);
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd aktualizacji pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pobiera informacje o cyklu uprawowym dla danego pola.
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <returns>Obiekt cyklu uprawowego.</returns>
    [HttpGet("getCycle/{fieldId}")]
    public async Task<IActionResult> GetCycle(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        var cycle = await _fieldService.GetCycleAsync(username, fieldId);
        return Ok(cycle);
    }

    /// <summary>
    /// Pobiera słownik dostępnych roślin i upraw.
    /// </summary>
    /// <returns>Lista dostępnych roślin.</returns>
    [HttpGet("getPlantsList")]
    public async Task<IActionResult> GetPlantsList()
    {
        var plants = await _fieldService.GetAllPlantsAsync();
        return Ok(plants);
    }

    /// <summary>
    /// Generuje wizualizację (PNG) najnowszego dostępnego skanu dla pola.
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="request">Opcjonalnie GeoJSON do przycięcia obrazu.</param>
    /// <returns>Plik obrazu (image/png) oraz data skanu w nagłówku 'X-Scan-Date'.</returns>
    [HttpPost("latestScan/{fieldId}")]
    public async Task<IActionResult> GetLatestScan(int fieldId, [FromBody] ScanRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetVisualizedScanAsync(username, fieldId, null, request.Geojson);

            if (result.PngBytes == null)
            {
                _logger.LogInformation("Brak skanów dla pola {FieldId}", fieldId);
                Response.Headers.Append("X-Scan-Date", "Brak danych");
                return NoContent();
            }

            Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
            return File(result.PngBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd wizualizacji najnowszego skanu dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generuje wizualizację konkretnego skanu na podstawie jego ID.
    /// </summary>
    /// <param name="scanId">ID skanu.</param>
    /// <param name="request">Opcjonalnie GeoJSON do przycięcia.</param>
    /// <returns>Plik obrazu (image/png).</returns>
    [HttpPost("imageById/{scanId}")]
    public async Task<IActionResult> GetScanById(int scanId, [FromBody] ScanRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetVisualizedScanAsync(username, 0, scanId, request.Geojson);
            if (result.PngBytes == null) return NoContent();

            Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
            return File(result.PngBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd wizualizacji skanu {ScanId}", scanId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Przesyła i przetwarza nowy plik skanu (ZIP z plikami TIFF).
    /// </summary>
    /// <param name="fieldId">ID pola, do którego przypisany jest skan.</param>
    /// <param name="data">Formularz z plikiem ZIP.</param>
    /// <returns>Komunikat o sukcesie.</returns>
    [HttpPost("uploadScan/{fieldId}")]
    public async Task<IActionResult> UploadScan(int fieldId, [FromForm] UpdateTiffsDto data)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (data.Zip == null || data.Zip.Length == 0)
            return BadRequest("Nie przesłano pliku ZIP.");

        _logger.LogInformation("Rozpoczęto upload skanu dla pola {FieldId} przez {Username}", fieldId, username);

        try
        {
            await _analysisService.ProcessAndSaveScanAsync(username, fieldId, data);
            _logger.LogInformation("Pomyślnie przetworzono skan dla pola {FieldId}", fieldId);
            return Ok(new { message = "Skan przetworzony i zapisany pomyślnie." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd przetwarzania skanu (GDAL/DB) dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pobiera historię wszystkich skanów dostępnych dla danego pola.
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <returns>Lista metadanych skanów.</returns>
    [HttpGet("getScansHistory/{fieldId}")]
    public async Task<IActionResult> GetScansHistory(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var scans = await _fieldService.GetScansHistoryAsync(username, fieldId);
            return Ok(scans);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania historii skanów pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Usuwa wybrany skan.
    /// </summary>
    /// <param name="scanId">ID skanu do usunięcia.</param>
    /// <returns>Status operacji.</returns>
    [HttpDelete("deleteScan/{scanId}")]
    public async Task<IActionResult> DeleteScan(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var deleted = await _fieldService.DeleteScanAsync(username, scanId);

            if (!deleted) return NotFound(new { error = "Skan nie istnieje lub brak uprawnień" });

            _logger.LogInformation("Usunięto skan {ScanId} przez {Username}", scanId, username);
            return Ok(new { message = "Skan został usunięty" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd usuwania skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Pobiera surowe dane numeryczne NDVI dla najnowszego skanu.
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <returns>Macierz wartości NDVI lub No Content.</returns>
    [HttpGet("latestNDVIData/{fieldId}")]
    public async Task<IActionResult> GetLatestNDVIData(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetNdviDataAsync(username, fieldId, null);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych NDVI dla pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Pobiera surowe dane numeryczne NDVI dla konkretnego skanu.
    /// </summary>
    /// <param name="scanId">ID skanu.</param>
    /// <returns>Macierz wartości NDVI.</returns>
    [HttpGet("NDVIDataById/{scanId}")]
    public async Task<IActionResult> GetNDVIDataById(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetNdviDataAsync(username, 0, scanId);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych numerycznych NDVI dla skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    [HttpGet("latestGNDVIData/{fieldId}")]
    public async Task<IActionResult> GetLatestGNDVIData(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetGndviDataAsync(username, fieldId, null);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych GNDVI dla pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    [HttpGet("GNDVIDataById/{scanId}")]
    public async Task<IActionResult> GetGNDVIDataById(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetGndviDataAsync(username, 0, scanId);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych numerycznych GNDVI dla skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    [HttpGet("latestSAVIData/{fieldId}")]
    public async Task<IActionResult> GetLatestSAVIData(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetSaviDataAsync(username, fieldId, null);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych SAVI dla pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    [HttpGet("SAVIDataById/{scanId}")]
    public async Task<IActionResult> GetSAVIDataById(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetSaviDataAsync(username, 0, scanId);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych numerycznych SAVI dla skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    [HttpGet("latestNDWIData/{fieldId}")]
    public async Task<IActionResult> GetLatestNDWIData(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetNdwiDataAsync(username, fieldId, null);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych NDWI dla pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    [HttpGet("NDWIDataById/{scanId}")]
    public async Task<IActionResult> GetNDWIDataById(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetNdwiDataAsync(username, 0, scanId);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych numerycznych NDWI dla skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    [HttpGet("latestEVIData/{fieldId}")]
    public async Task<IActionResult> GetLatestEVIData(int fieldId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetEviDataAsync(username, fieldId, null);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych EVI dla pola {FieldId}", fieldId);
            return StatusCode(500);
        }
    }

    [HttpGet("EVIDataById/{scanId}")]
    public async Task<IActionResult> GetEVIDataById(int scanId)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        try
        {
            var result = await _analysisService.GetEviDataAsync(username, 0, scanId);
            return OkIndexData(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd pobierania danych numerycznych EVI dla skanu {ScanId}", scanId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Renderuje obraz NDVI na podstawie przesłanej macierzy danych (operacja bezstanowa).
    /// </summary>
    /// <param name="request">Macierz wartości NDVI i parametry palety kolorów.</param>
    /// <returns>Wygenerowany obraz PNG.</returns>
    [HttpPost("visualize")]
    public IActionResult VisualizeNDVI([FromBody] IndexVisualizationDto request)
    {
        if (request.IndexMatrix == null || request.IndexMatrix.Length == 0)
            return BadRequest("Brak danych NDVI");

        if (!TryNormalizeIndexDimensions(request.IndexMatrix, request.MatrixWidth, request.MatrixHeight, out var width, out var height, out var validationError))
            return BadRequest(validationError);

        try
        {
            var normalizedRequest = request with
            {
                MatrixWidth = width,
                MatrixHeight = height
            };

            var imageBytes = _analysisService.RenderIndexVisualization(normalizedRequest);
            return File(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd renderowania wizualizacji NDVI");
            return BadRequest(new { error = ex.Message });
        }
    }

    private static bool TryNormalizeIndexDimensions(float[] indexMatrix, int matrixWidth, int matrixHeight, out int width, out int height, out string error)
    {
        width = matrixWidth;
        height = matrixHeight;
        error = string.Empty;

        if (width > 0 && height > 0)
        {
            if (indexMatrix.Length != width * height)
            {
                error = "Długość macierzy NDVI nie odpowiada podanym wymiarom.";
                return false;
            }

            return true;
        }

        if (width <= 0 && height <= 0)
        {
            var square = (int)Math.Sqrt(indexMatrix.Length);
            if (square > 0 && square * square == indexMatrix.Length)
            {
                width = square;
                height = square;
                return true;
            }

            error = "Brak poprawnych wymiarów macierzy NDVI.";
            return false;
        }

        if (width <= 0 && height > 0 && indexMatrix.Length % height == 0)
        {
            width = indexMatrix.Length / height;
            return true;
        }

        if (height <= 0 && width > 0 && indexMatrix.Length % width == 0)
        {
            height = indexMatrix.Length / width;
            return true;
        }

        error = "Długość macierzy NDVI nie odpowiada podanym wymiarom.";
        return false;
    }

    /// <summary>
    /// Wykonuje grupowanie (klasteryzację) ryzyka na polu.
    /// </summary>
    /// <param name="fieldId">ID pola.</param>
    /// <param name="request">Parametry grupowania.</param>
    /// <returns>Wynik analizy klastrowania.</returns>
    [HttpPost("group/{fieldId}")]
    public async Task<IActionResult> Group(int fieldId, [FromBody] NdviGroupRequestDto request)
    {
        var username = GetCurrentUsername();
        if (string.IsNullOrEmpty(username)) return Unauthorized();

        if (request == null || request.CycleId == 0 || request.PlantId == 0)
            return BadRequest("Niepoprawne dane wejściowe.");

        try
        {
            var result = await _analysisService.GroupRiskAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd klastrowania dla pola {FieldId}", fieldId);
            return BadRequest(new { error = ex.Message });
        }
    }
}
