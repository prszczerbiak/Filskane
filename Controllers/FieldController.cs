using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;   // Tu są Twoje rekordy (DTO)
using WebApplication1.Services; // Tu są Twoje serwisy

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/field")]
    [Authorize]
    public class FieldController : ControllerBase
    {
        private readonly FieldService _fieldService;
        private readonly AnalysisService _analysisService;

        public FieldController(FieldService fieldService, AnalysisService analysisService)
        {
            _fieldService = fieldService;
            _analysisService = analysisService;
        }

        // Helper do wyciągania loginu
        private string GetCurrentUsername() => User.Identity?.Name ?? string.Empty;

        // ==========================================
        // 1. ZARZĄDZANIE POLEM (CRUD)
        // ==========================================

        [HttpGet("getData/{fieldId}")]
        public async Task<IActionResult> GetFieldInfo(int fieldId)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            // Zmiana: _db -> _fieldService
            var field = await _fieldService.GetFieldDetailsAsync(username, fieldId);

            if (field == null) return NotFound("Nie znaleziono pola");

            return Ok(field);
        }

        [HttpPut("update/{fieldId}")]
        public async Task<IActionResult> UpdateFieldInfo(int fieldId, [FromBody] UpdateFieldRequest request)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                Console.WriteLine(request.CropId);
                // Zmiana: _db -> _fieldService
                await _fieldService.UpdateFieldAsync(fieldId, request);
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("getCycle/{fieldId}")]
        public async Task<IActionResult> GetCycle(int fieldId)
        {
            // Zmiana: _db -> _fieldService
            var cycle = await _fieldService.GetCycleAsync(fieldId);

            //if (cycle == null) return NotFound(new { error = "Nie znaleziono cyklu" });

            return Ok(cycle);
        }

        [HttpGet("getPlantsList")]
        public async Task<IActionResult> GetPlantsList()
        {
            // Zmiana: _db -> _fieldService
            var plants = await _fieldService.GetAllPlantsAsync();
            return Ok(plants);
        }

        // ==========================================
        // 2. OBRAZY I SKANY (TIFF -> PNG)
        // ==========================================

        [HttpPost("latestScan/{fieldId}")]
        public async Task<IActionResult> GetLatestScan(int fieldId, [FromBody] ScanRequestDto request)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                // Cała magia (pobranie z bazy + konwersja + rysowanie) dzieje się w serwisie
                // fieldId: podajemy, scanId: null (bo chcemy najnowszy)
                var result = await _analysisService.GetVisualizedScanAsync(fieldId, null, request.Geojson);

                if (result.PngBytes == null)
                {
                    Response.Headers.Append("X-Scan-Date", "Brak danych");
                    return NoContent();
                }

                Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));

                // Zwracamy gotowy plik PNG
                return File(result.PngBytes, "image/png");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("imageById/{scanId}")]
        public async Task<IActionResult> GetScanById(int scanId, [FromBody] ScanRequestDto request)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            try
            {
                // fieldId: 0 (nieistotne, bo szukamy po ID skanu), scanId: podajemy
                var result = await _analysisService.GetVisualizedScanAsync(0, scanId, request.Geojson);

                if (result.PngBytes == null) return NoContent();

                Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
                return File(result.PngBytes, "image/png");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("uploadScan/{fieldId}")]
        public async Task<IActionResult> UploadScan(int fieldId, [FromForm] UpdateTiffsDto data)
        {
            var username = GetCurrentUsername();
            if (string.IsNullOrEmpty(username)) return Unauthorized();

            if (data.Zip == null || data.Zip.Length == 0)
                return BadRequest("Nie przesłano pliku ZIP.");

            try
            {
                // Kontroler tylko przekazuje plik. 
                // GDAL, rozpakowywanie i zapis do Oracle dzieje się w serwisie.
                await _analysisService.ProcessAndSaveScanAsync(fieldId, data);

                return Ok(new { message = "Skan przetworzony i zapisany pomyślnie." });
            }
            catch (Exception ex)
            {
                // Łapiemy błędy walidacji (np. "Brakuje pasm") lub błędy GDAL
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("getScansHistory/{fieldId}")]
        public async Task<IActionResult> GetScansHistory(int fieldId)
        {
            // Zmiana: _db -> _fieldService
            var scans = await _fieldService.GetScansHistoryAsync(fieldId);
            return Ok(scans);
        }

        [HttpDelete("deleteScan/{scanId}")]
        public async Task<IActionResult> DeleteScan(int scanId)
        {
            // Zmiana: _db -> _fieldService
            var deleted = await _fieldService.DeleteScanAsync(scanId);
            if (!deleted) return NotFound(new { error = "Skan nie istnieje" });

            return Ok(new { message = "Skan został usunięty" });
        }

        // ==========================================
        // 3. DANE NDVI I ANALIZA RYZYKA
        // ==========================================

        [HttpGet("latestNDVIData/{fieldId}")]
        public async Task<IActionResult> GetLatestNDVIData(int fieldId)
        {
            // Pobieramy surowe dane liczbowe (macierz) dla wykresów JS
            var result = await _analysisService.GetNdviDataAsync(fieldId, null);

            if (result.Data == null)
            {
                Response.Headers.Append("X-Scan-Date", "Brak danych");
                return NoContent();
            }

            Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
            return Ok(result.Data); // Zwraca JSON: { ndvi: [[0.1, 0.2]...], fieldBbox: "..." }
        }

        [HttpGet("NDVIDataById/{scanId}")]
        public async Task<IActionResult> GetNDVIDataById(int scanId)
        {
            var result = await _analysisService.GetNdviDataAsync(0, scanId);

            if (result.Data == null) return NoContent();

            Response.Headers.Append("X-Scan-Date", result.Date?.ToString("yyyy-MM-dd"));
            return Ok(result.Data);
        }

        [HttpPost("visualize")]
        public IActionResult VisualizeNDVI([FromBody] NdviVisualizationDto request)
        {
            try
            {
                if (request.NdviMatrix == null || request.NdviMatrix.Count == 0)
                    return BadRequest("Brak danych NDVI");

                // Generowanie heatmapy z surowych danych
                var imageBytes = _analysisService.RenderNdviVisualization(request);

                return File(imageBytes, "image/png");
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("group/{fieldId}")]
        public async Task<IActionResult> Group(int fieldId, [FromBody] NdviGroupRequest request)
        {
            if (request == null || request.CycleId == 0)
                return BadRequest("Niepoprawne dane wejściowe.");

            try
            {
                // Cała logika grupowania, nakładania warstw i legendy jest w serwisie
                var result = await _analysisService.GroupRiskAsync(request);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}